using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class NetArchitecturesApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Rules_bring_the_tables_with_sources_and_the_catalog_ice()
    {
        var body = (await _client.GetFromJsonAsync<JsonNode>("/api/net-architectures/rules"))!;

        body["rules"]!["lobbyTable"]!["source"]!.GetValue<string>().Should().Contain("CP:R p.211");
        body["rules"]!["bodyTables"]!["value"]!.AsArray().Should().HaveCount(4);
        body["rules"]!["defenses"]!["value"]!.AsArray().Should().HaveCount(18);
        body["ice"]!.AsArray().Should().HaveCount(12);
        body["ice"]!.AsArray().Should().Contain(i => i!["slug"]!.GetValue<string>() == "hellhound" && i["rez"]!.GetValue<int>() == 20);
    }

    [Fact]
    public async Task Architecture_lifecycle_generate_edit_export_delete()
    {
        var created = await _client.PostAsJsonAsync("/api/net-architectures",
            new { difficulty = "Standard", floors = 9, branches = 1, locationId = "corporate-office", seed = 77, enemyNetrunner = true });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var arch = (await created.Content.ReadFromJsonAsync<JsonNode>())!;
        var id = arch["id"]!.GetValue<int>();
        arch["state"]!["branches"]!.AsArray().Should().HaveCount(2);
        arch["quote"]!["total"]!.GetValue<int>().Should().BeGreaterThan(9 * 5000 - 1);
        arch["watchers"]!.AsArray().Should().Contain(w => w!["kind"]!.GetValue<string>() == "Netrunner");
        arch["difficulty"]!["dv"]!.GetValue<int>().Should().Be(8);

        (await _client.GetFromJsonAsync<JsonArray>("/api/net-architectures"))!.Should().Contain(a => a!["id"]!.GetValue<int>() == id);

        var version = arch["version"]!.GetValue<int>();
        var boss = await _client.PostAsJsonAsync($"/api/net-architectures/{id}/floors/A5/set", new { version, table = "Uncommon", roll = 9 });
        boss.StatusCode.Should().Be(HttpStatusCode.OK, await boss.Content.ReadAsStringAsync());
        var edited = (await boss.Content.ReadFromJsonAsync<JsonNode>())!;
        edited["version"]!.GetValue<int>().Should().Be(version + 1);

        var stale = await _client.PostAsJsonAsync($"/api/net-architectures/{id}/floors/A5/reroll", new { version });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var text = await _client.GetStringAsync($"/api/net-architectures/{id}/export?format=text");
        text.Should().Contain("(Backdoor DV10)").And.Contain("[Boss Floor]").And.Contain("Total:");
        (await _client.GetStringAsync($"/api/net-architectures/{id}/export?format=markdown")).Should().Contain("|---|");

        (await _client.DeleteAsync($"/api/net-architectures/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/net-architectures/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_endpoints_are_gone()
    {
        (await _client.PostAsJsonAsync("/api/admin/items", new { name = "x", category = "gear" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
