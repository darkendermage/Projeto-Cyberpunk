using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class NightMarketsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonNode> Create(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/night-markets", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    [Fact]
    public async Task Rules_list_the_presets_books_and_index_sections()
    {
        var rules = (await _client.GetFromJsonAsync<JsonNode>("/api/night-markets/rules"))!;

        rules["presets"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).Should().Contain(["core", "bc-a", "bc-f", "toggle", "mr-amaze"]);
        rules["books"]!.AsArray().Should().Contain(b => b!["name"]!.GetValue<string>() == "Black Chrome");
        rules["indexSections"]!.AsArray().Should().HaveCount(10);
        rules["rules"]!["generator"]!["goods"]!["source"]!.GetValue<string>().Should().Be("CP:R p.338–339");
    }

    [Fact]
    public async Task A_core_market_is_generated_saved_and_reopened()
    {
        var created = await Create(new { presetId = "core", seed = 77, name = "Feira de teste" });

        var id = created["id"]!.GetValue<int>();
        created["goods"]!.AsArray().Should().HaveCount(2);
        created["books"]!.AsArray().Select(b => b!.GetValue<string>()).Should().Equal("Core Book");
        var again = (await _client.GetFromJsonAsync<JsonNode>($"/api/night-markets/{id}"))!;
        again["listings"]!.AsArray().Count.Should().Be(created["listings"]!.AsArray().Count);
        again["name"]!.GetValue<string>().Should().Be("Feira de teste");

        var list = (await _client.GetFromJsonAsync<JsonArray>("/api/night-markets"))!;
        list.Should().Contain(m => m!["id"]!.GetValue<int>() == id);
    }

    [Fact]
    public async Task Purchase_with_haggle_goes_to_the_ledger_and_a_stale_version_is_refused()
    {
        var market = await Create(new { presetId = "bc-f", seed = 9 });
        var id = market["id"]!.GetValue<int>();
        var version = market["version"]!.GetValue<int>();
        var listing = market["listings"]!.AsArray().First(l => l!["forSale"]!.GetValue<bool>() && l["price"]!.GetValue<int>() >= 100)!;
        var price = listing["price"]!.GetValue<int>();

        var response = await _client.PostAsJsonAsync($"/api/night-markets/{id}/purchase", new
        {
            version, listingId = listing["id"]!.GetValue<int>(), quantity = 1, buyer = "Rogue",
            haggle = new { buyer = new { trading = 15, rank = 9 }, seller = new { trading = 16, rank = 8 }, buyerRolls = new[] { 9 }, sellerRolls = new[] { 2 } },
            dealId = "discount20",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var after = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var sale = after["sales"]!.AsArray().Single()!;
        sale["total"]!.GetValue<int>().Should().Be(price - price * 20 / 100);
        sale["buyer"]!.GetValue<string>().Should().Be("Rogue");

        var stale = await _client.PostAsJsonAsync($"/api/night-markets/{id}/purchase", new { version, listingId = listing["id"]!.GetValue<int>() });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_night_market_needs_an_organizer_of_rank_five()
    {
        var response = await _client.PostAsJsonAsync("/api/night-markets", new { presetId = "core", organizer = new { name = "Novato", operatorRank = 2 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Operator 5");
    }

    [Fact]
    public async Task Index_pages_and_links_to_the_catalog()
    {
        var page = (await _client.GetFromJsonAsync<JsonNode>("/api/night-markets/index?section=weapons&q=medium%20pistol&pageSize=10"))!;

        page["total"]!.GetValue<int>().Should().BeGreaterThan(0);
        var row = page["rows"]!.AsArray().First(r => r!["name"]!.GetValue<string>() == "Medium Pistol")!;
        row["itemId"].Should().NotBeNull();
        row["priceCategory"]!.GetValue<string>().Should().Be("Costly");

        var extras = (await _client.GetFromJsonAsync<JsonNode>("/api/night-markets/index?catalogued=false&pageSize=5"))!;
        extras["rows"]!.AsArray().Should().OnlyContain(r => r!["itemId"] == null);
    }

    [Fact]
    public async Task Mystery_box_and_door_prize_events()
    {
        var box = (await (await _client.PostAsJsonAsync("/api/night-markets/mystery-box", new { boxRoll = 9, contentRoll = 6 }))
            .Content.ReadFromJsonAsync<JsonNode>())!;
        box["prize"]!["label"]!.GetValue<string>().Should().Be("Smartgun Link");

        var molly = await Create(new { presetId = "bc-c", seed = 4 });
        // Black Chrome+ stock, found by slug in the real database.
        molly["listings"]!.AsArray().Count(l => l!["origin"]!.GetValue<string>() == "Preset").Should().Be(12);
        var response = await _client.PostAsJsonAsync($"/api/night-markets/{molly["id"]}/door-prize",
            new { version = molly["version"]!.GetValue<int>(), winner = "Nova", roll = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        result["result"]!["prize"]!["quantity"]!.GetValue<int>().Should().Be(20);
        result["market"]!["events"]!.AsArray().Should().Contain(e => e!["kind"]!.GetValue<string>() == "door-prize");
    }
}
