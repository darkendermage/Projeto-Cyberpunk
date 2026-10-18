using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class WriteApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Item_lifecycle_create_update_search_delete()
    {
        var slug = $"teste-{Guid.NewGuid():N}"[..20];
        var create = await _client.PostAsJsonAsync("/api/admin/items", new
        {
            slug,
            name = "Pistola de Teste Zeta",
            category = "weapons",
            price = 123,
            manufacturer = "Fabricante de Teste",
            tags = new[] { "Teste" },
            attributes = new { weaponClass = "HeavyPistol", rof = 2, damage = "3d6", damageDice = 3, calibers = new[] { "HeavyPistol" } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var item = (await create.Content.ReadFromJsonAsync<JsonNode>())!;
        var id = item["id"]!.GetValue<int>();
        item["dataOrigin"]!.GetValue<string>().Should().Be("Custom");

        // searchable by manufacturer, and filterable by its attributes
        var search = await _client.PostAsJsonAsync("/api/catalog/weapons/search", new
        {
            q = "zeta fabricante",
            filters = new object[] { new { field = "manufacturer", op = "in", values = new[] { "Fabricante de Teste" } } },
        });
        (await search.Content.ReadFromJsonAsync<JsonNode>())!["total"]!.GetValue<int>().Should().Be(1);

        var update = await _client.PutAsJsonAsync($"/api/admin/items/{id}", new
        {
            name = "Pistola de Teste Zeta II",
            category = "weapons",
            price = 150,
            attributes = new { weaponClass = "HeavyPistol", rof = 1, calibers = new[] { "HeavyPistol", "VeryHeavyPistol" } },
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        var updated = (await update.Content.ReadFromJsonAsync<JsonNode>())!;
        updated["attributes"]!["rof"]!.GetValue<int>().Should().Be(1);
        updated["attributes"]!["calibers"]!.AsArray().Should().HaveCount(2);

        (await _client.DeleteAsync($"/api/admin/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/items/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_item_is_rejected_with_per_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/items", new
        {
            name = "",
            category = "armor",
            price = -5,
            attributes = new { armorType = "Clothing", clothingSlot = "Top", sp = 11 },
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonNode>())!["errors"]!.AsObject().Select(e => e.Key);
        errors.Should().Contain(["name", "price", "attributes.sp"]);
    }

    [Fact]
    public async Task Export_then_import_is_an_idempotent_round_trip()
    {
        var exported = await _client.GetFromJsonAsync<JsonArray>("/api/admin/export?category=drugs");
        exported!.Should().NotBeEmpty();

        var import = await _client.PostAsJsonAsync("/api/admin/import", exported);
        var result = (await import.Content.ReadFromJsonAsync<JsonNode>())!;
        result["created"]!.GetValue<int>().Should().Be(0);
        result["updated"]!.GetValue<int>().Should().Be(exported.Count);
        result["failed"]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    public async Task Csv_export_has_header_and_one_line_per_item()
    {
        var csv = await _client.GetStringAsync("/api/admin/export?category=hq-upgrades&format=csv");
        var lines = csv.Trim().Split('\n');
        lines[0].Should().Contain("slug;nome;preco_eb");
        lines.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Favorites_and_lists_flow()
    {
        var weapon = (await (await _client.PostAsJsonAsync("/api/catalog/weapons/search", new { pageSize = 1 }))
            .Content.ReadFromJsonAsync<JsonNode>())!["items"]![0]!["id"]!.GetValue<int>();

        (await _client.PutAsync($"/api/favorites/{weapon}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetFromJsonAsync<int[]>("/api/favorites/ids")).Should().Contain(weapon);

        var list = (await (await _client.PostAsJsonAsync("/api/lists", new { name = "Loadout de teste" })).Content.ReadFromJsonAsync<JsonNode>())!;
        var listId = list["id"]!.GetValue<int>();
        await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 2 });
        var detail = (await (await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 1 }))
            .Content.ReadFromJsonAsync<JsonNode>())!;
        detail["entries"]!.AsArray().Should().ContainSingle().Which!["quantity"]!.GetValue<int>().Should().Be(3);

        var invalid = await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 0 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.DeleteAsync($"/api/lists/{listId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/favorites/{weapon}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
