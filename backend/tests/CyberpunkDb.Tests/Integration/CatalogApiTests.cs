using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class CatalogApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonNode> Search(string category, object body)
    {
        var response = await _client.PostAsJsonAsync($"/api/catalog/{category}/search", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    [Fact]
    public async Task Every_category_is_listed_with_seeded_items()
    {
        var categories = await _client.GetFromJsonAsync<JsonArray>("/api/categories");
        categories!.Should().HaveCount(11);
        categories.Select(c => c!["itemCount"]!.GetValue<int>()).Should().OnlyContain(n => n > 0);
    }

    [Fact]
    public async Task Schema_exposes_filters_with_numeric_bounds()
    {
        var schema = await _client.GetFromJsonAsync<JsonNode>("/api/categories/weapons/schema");
        var rof = schema!["fields"]!.AsArray().Single(f => f!["key"]!.GetValue<string>() == "rof")!;
        rof["filterable"]!.GetValue<bool>().Should().BeTrue();
        rof["min"]!.GetValue<double>().Should().BeGreaterThan(0);
        rof["operators"]!.AsArray().Select(o => o!.GetValue<string>()).Should().Contain(["eq", "between"]);
    }

    [Fact]
    public async Task Heavy_pistol_with_rof_2_returns_only_matching_items()
    {
        var result = await Search("weapons", new
        {
            filters = new object[]
            {
                new { field = "weaponClass", op = "in", values = new[] { "HeavyPistol" } },
                new { field = "rof", op = "eq", value = 2 },
            },
            pageSize = 100,
        });

        var items = result["items"]!.AsArray();
        items.Should().NotBeEmpty();
        result["total"]!.GetValue<int>().Should().Be(items.Count);
        items.Should().OnlyContain(i =>
            i!["subtype"]!["value"]!.GetValue<string>() == "HeavyPistol" && i["attributes"]!["rof"]!.GetValue<int>() == 2);
    }

    [Fact]
    public async Task Facet_counts_match_the_results_they_lead_to()
    {
        var result = await Search("weapons", new { filters = new[] { new { field = "rof", op = "eq", value = 2 } } });
        var pistolFacet = result["facets"]!["weaponClass"]!.AsArray().Single(f => f!["value"]!.GetValue<string>() == "HeavyPistol")!;

        var narrowed = await Search("weapons", new
        {
            filters = new object[]
            {
                new { field = "rof", op = "eq", value = 2 },
                new { field = "weaponClass", op = "in", values = new[] { "HeavyPistol" } },
            },
        });
        narrowed["total"]!.GetValue<int>().Should().Be(pistolFacet["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task Vehicles_cheaper_than_50000_and_sorted_by_price()
    {
        var result = await Search("vehicles", new
        {
            filters = new[] { new { field = "price", op = "lt", value = 50000 } },
            sort = "price",
            dir = "desc",
        });
        var prices = result["items"]!.AsArray().Select(i => i!["price"]!.GetValue<int>()).ToList();
        prices.Should().NotBeEmpty().And.OnlyContain(p => p < 50000).And.BeInDescendingOrder();
    }

    [Fact]
    public async Task Paging_is_stable_and_complete()
    {
        var first = await Search("cyberware", new { pageSize = 50, page = 1, includeFacets = false });
        var total = first["total"]!.GetValue<int>();
        var ids = new HashSet<int>();
        for (var page = 1; page <= first["totalPages"]!.GetValue<int>(); page++)
        {
            var result = await Search("cyberware", new { pageSize = 50, page, includeFacets = false });
            foreach (var item in result["items"]!.AsArray()) ids.Add(item!["id"]!.GetValue<int>()).Should().BeTrue("no item should repeat across pages");
        }
        ids.Should().HaveCount(total);
    }

    [Fact]
    public async Task Invalid_filters_return_problem_details_with_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/weapons/search", new
        {
            filters = new object[] { new { field = "rof", op = "in", values = new[] { 1 } }, new { field = "nope", op = "eq", value = 1 } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        problem!["errors"]!.AsObject().Select(e => e.Key).Should().Contain(["rof", "nope"]);
    }

    [Fact]
    public async Task Unknown_category_returns_404()
    {
        (await _client.GetAsync("/api/categories/spaceships/schema")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Global_search_groups_results_by_category()
    {
        var groups = await _client.GetFromJsonAsync<JsonArray>("/api/catalog/search?q=militech");
        groups!.Select(g => g!["category"]!.GetValue<string>()).Should().Contain("weapons");
    }

    [Fact]
    public async Task Weapon_detail_lists_compatible_ammunition()
    {
        var weapon = (await Search("weapons", new { q = "militech avenger" }))["items"]![0]!;
        var detail = await _client.GetFromJsonAsync<JsonNode>($"/api/items/{weapon["id"]}");
        detail!["relations"]!.AsArray().Select(r => r!["key"]!.GetValue<string>()).Should().Contain("compatibleAmmo");
    }

    [Fact]
    public async Task Data_origin_is_exposed_on_every_item()
    {
        // Fashionware lives in Cyberware (CP:R p.111) and carries its book page.
        var result = await Search("cyberware", new
        {
            filters = new object[] { new { field = "cyberwareType", op = "in", values = new[] { "Fashionware" } } },
            pageSize = 50,
        });
        result["items"]!.AsArray().Should().HaveCount(11).And
            .OnlyContain(i => i!["dataOrigin"]!.GetValue<string>() == "Verified" && i["page"] != null);
    }

    [Fact]
    public async Task Every_seed_item_is_imported()
    {
        var seed = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")))!.AsArray();
        var categories = (await _client.GetFromJsonAsync<JsonNode>("/api/categories"))!.AsArray();

        foreach (var group in seed.GroupBy(i => i!["category"]!.GetValue<string>()))
        {
            var category = categories.FirstOrDefault(c => c!["key"]!.GetValue<string>() == group.Key);
            category.Should().NotBeNull($"a categoria {group.Key} do seed precisa existir");
            category!["itemCount"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(group.Count(), group.Key);
        }
        categories.Select(c => c!["key"]!.GetValue<string>()).Should().NotContain("fashionware").And.Contain("netrunning");
    }

    [Fact]
    public async Task Netrunning_tab_has_programs_black_ice_decks_and_hardware()
    {
        var ice = await Search("netrunning", new
        {
            filters = new object[] { new { field = "netType", op = "in", values = new[] { "BlackIce" } } },
            sort = "rez", dir = "desc", pageSize = 50,
        });
        ice["total"]!.GetValue<int>().Should().Be(12);
        var kraken = ice["items"]![0]!;
        kraken["name"]!.GetValue<string>().Should().BeOneOf("Kraken", "Dragon");
        kraken["attributes"]!["rez"]!.GetValue<int>().Should().Be(30);
        kraken["attributes"]!["slotsRequired"]!.GetValue<int>().Should().Be(2);

        var decks = await Search("netrunning", new
        {
            q = "cyberdeck",
            filters = new object[] { new { field = "netType", op = "in", values = new[] { "Cyberdeck" } } },
            sort = "totalSlots", dir = "desc", pageSize = 50,
        });
        decks["items"]!.AsArray().Select(i => i!["attributes"]!["totalSlots"]!.GetValue<int>()).Should().Contain([5, 7, 9]);

        (await Search("gear", new { q = "hellhound" }))["total"]!.GetValue<int>().Should().Be(0, "programas saíram de Itens Avulsos");
    }

    [Fact]
    public async Task Vehicles_filter_by_nomad_moto_rank()
    {
        var result = await Search("vehicles", new
        {
            filters = new object[] { new { field = "motoRank", op = "lte", value = 4 } },
            pageSize = 100,
        });
        var names = result["items"]!.AsArray().Select(i => i!["name"]!.GetValue<string>()).ToList();
        names.Should().Contain(["Compact Groundcar", "Roadbike", "Jetski", "Gyrocopter", "The Grundy"]);
        names.Should().NotContain(["Helicopter", "AV-9 Super Aerodyne", "Bicycle"]);
        result["items"]!.AsArray().Should().OnlyContain(i => i!["attributes"]!["motoRank"]!.GetValue<int>() <= 4);
    }

    [Fact]
    public async Task Weapon_upgrades_are_tagged_and_filterable_by_compatible_weapon()
    {
        var forPistols = await Search("weapon-attachments", new
        {
            filters = new object[] { new { field = "compatibleClasses", op = "in", values = new[] { "HeavyPistol" } } },
            pageSize = 50,
        });
        var slugs = forPistols["items"]!.AsArray().Select(i => i!["slug"]!.GetValue<string>()).ToList();
        slugs.Should().Contain(["pistol-autosear", "smartgun-link", "silencer"]).And.NotContain(["bayonet", "reinforced-string"]);

        var bayonet = (await Search("weapon-attachments", new { q = "bayonet" }))["items"]!.AsArray()
            .First(i => i!["slug"]!.GetValue<string>() == "bayonet")!;
        bayonet["attributes"]!["compatibleClasses"]!.AsArray().Select(v => v!.GetValue<string>())
            .Should().BeEquivalentTo(["Shotgun", "AssaultRifle", "SniperRifle"]);

        var pistol = (await Search("weapons", new { q = "heavy pistol" }))["items"]!.AsArray()
            .First(i => i!["slug"]!.GetValue<string>() == "heavy-pistol")!;
        var detail = await _client.GetFromJsonAsync<JsonNode>($"/api/items/{pistol["id"]}");
        var upgrades = detail!["relations"]!.AsArray().First(r => r!["key"]!.GetValue<string>() == "compatibleUpgrades")!;
        upgrades["items"]!.AsArray().Select(i => i!["slug"]!.GetValue<string>()).Should().Contain("pistol-autosear");
    }
}
