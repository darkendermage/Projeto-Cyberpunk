using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class NpcsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Rules_expose_every_template_with_sources()
    {
        var rules = (await _client.GetFromJsonAsync<JsonNode>("/api/npcs/rules"))!;

        var templates = rules["templates"]!.AsArray();
        templates.Should().HaveCount(8);
        var mook = templates.First(t => t!["level"]!.GetValue<string>() == "Mook" && !t["hardened"]!.GetValue<bool>())!;
        mook["values"]!["statPoints"]!.GetValue<int>().Should().Be(33);
        mook["facts"]!.AsArray().Should().OnlyContain(f => f!["source"] != null || f["complementary"]!.GetValue<bool>());
        rules["skills"]!.AsArray().Should().HaveCount(66);
    }

    [Fact]
    public async Task Generator_produces_valid_sheets_from_the_real_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NpcService>();
        var rules = scope.ServiceProvider.GetRequiredService<NpcRuleBook>();
        var failures = new List<string>();

        foreach (var level in Enum.GetValues<NpcLevel>())
        foreach (var hardened in new[] { false, true })
        foreach (var profile in rules.Profiles)
        for (var seed = 1; seed <= 6; seed++)
        {
            var result = await service.GenerateAsync(new NpcGenerationRequest { Level = level, Hardened = hardened, ProfileId = profile.Id, Seed = seed }, default);
            if (!result.Success)
                failures.Add($"{level}{(hardened ? "+H" : "")}/{profile.Id}/{seed}: {string.Join(" | ", result.Conflicts.Select(c => c.Message))}");
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_endpoint_returns_sheet_report_and_seed()
    {
        var response = await _client.PostAsJsonAsync("/api/npcs/generate", new { level = "Lieutenant", profileId = "netrunner", seed = 42 });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        body["success"]!.GetValue<bool>().Should().BeTrue();
        body["seed"]!.GetValue<int>().Should().Be(42);
        body["report"]!["status"]!.GetValue<string>().Should().Be("Valid");
        body["report"]!["stats"]!["total"]!.GetValue<int>().Should().Be(44);
        body["sheet"]!["level"]!.GetValue<string>().Should().Be("Lieutenant");
    }

    [Fact]
    public async Task Required_and_forbidden_cyberware_by_id()
    {
        var ids = await SlugIdsAsync("neural-link", "interface-plugs");

        var response = await _client.PostAsJsonAsync("/api/npcs/generate", new
        {
            level = "Mook", hardened = true, profileId = "netrunner", seed = 7,
            cyberwareRules = new[] { new { itemId = ids["interface-plugs"], state = "Required" } },
        });
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        body["success"]!.GetValue<bool>().Should().BeTrue(body.ToJsonString());
        var installed = body["sheet"]!["cyberware"]!.AsArray().Select(c => c!["itemId"]!.GetValue<int>()).ToList();
        installed.Should().Contain([ids["interface-plugs"], ids["neural-link"]]);

        var impossible = await _client.PostAsJsonAsync("/api/npcs/generate", new
        {
            level = "Mook", seed = 7,
            cyberwareRules = new object[]
            {
                new { itemId = ids["interface-plugs"], state = "Required" },
                new { itemId = ids["neural-link"], state = "Forbidden" },
            },
        });
        var failure = (await impossible.Content.ReadFromJsonAsync<JsonNode>())!;
        failure["success"]!.GetValue<bool>().Should().BeFalse();
        failure["conflicts"]!.AsArray().Should().Contain(c => c!["code"]!.GetValue<string>() == "CYBER_REQUIRED_IMPOSSIBLE");
    }

    [Fact]
    public async Task Npc_lifecycle_save_edit_duplicate_delete()
    {
        var generated = (await (await _client.PostAsJsonAsync("/api/npcs/generate", new { level = "Mook", seed = 11 })).Content.ReadFromJsonAsync<JsonNode>())!;
        var sheet = generated["sheet"]!.AsObject();
        sheet["name"] = "Teste Lifecycle";

        var create = await _client.PostAsJsonAsync("/api/npcs", new JsonObject { ["sheet"] = sheet.DeepClone() });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var saved = (await create.Content.ReadFromJsonAsync<JsonNode>())!;
        var id = saved["id"]!.GetValue<int>();
        saved["status"]!.GetValue<string>().Should().Be("Valid");

        // Edit: concept change keeps it valid.
        sheet["concept"] = "Guarda de boate";
        var update = await _client.PutAsJsonAsync($"/api/npcs/{id}", new JsonObject { ["sheet"] = sheet.DeepClone() });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<JsonNode>())!["sheet"]!["concept"]!.GetValue<string>().Should().Be("Guarda de boate");

        var dup = await _client.PostAsync($"/api/npcs/{id}/duplicate", null);
        dup.StatusCode.Should().Be(HttpStatusCode.Created);
        var copy = (await dup.Content.ReadFromJsonAsync<JsonNode>())!;
        copy["sheet"]!["name"]!.GetValue<string>().Should().Be("Teste Lifecycle (cópia)");
        copy["id"]!.GetValue<int>().Should().NotBe(id);

        var list = (await _client.GetFromJsonAsync<JsonNode>("/api/npcs"))!.AsArray();
        list.Select(n => n!["id"]!.GetValue<int>()).Should().Contain([id, copy["id"]!.GetValue<int>()]);

        (await _client.DeleteAsync($"/api/npcs/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/npcs/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_sheet_is_only_saved_as_draft()
    {
        var generated = (await (await _client.PostAsJsonAsync("/api/npcs/generate", new { level = "Mook", seed = 12 })).Content.ReadFromJsonAsync<JsonNode>())!;
        var sheet = generated["sheet"]!.AsObject();
        sheet["name"] = "Mook Bombado";
        sheet["stats"]!["REF"] = 8; // above the Mook maximum and over budget

        var refused = await _client.PostAsJsonAsync("/api/npcs", new JsonObject { ["sheet"] = sheet.DeepClone() });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("não pode ser salva como válida");

        var draft = await _client.PostAsJsonAsync("/api/npcs", new JsonObject { ["sheet"] = sheet.DeepClone(), ["saveAsDraft"] = true });
        draft.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await draft.Content.ReadFromJsonAsync<JsonNode>())!;
        body["status"]!.GetValue<string>().Should().Be("Draft");
        body["report"]!["isValid"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Validate_endpoint_reports_budget_usage()
    {
        var response = await _client.PostAsJsonAsync("/api/npcs/validate", new
        {
            name = "Manual",
            level = "Mook",
            stats = new { INT = 3, REF = 6, DEX = 5, TECH = 2, COOL = 4, WILL = 4, MOVE = 3, BODY = 2, EMP = 2 },
        });
        var report = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        report["stats"]!["used"]!.GetValue<int>().Should().Be(31);
        report["stats"]!["remaining"]!.GetValue<int>().Should().Be(2);
        report["isValid"]!.GetValue<bool>().Should().BeTrue(report["issues"]!.ToJsonString());
    }

    [Fact]
    public async Task Enums_travel_as_strings_both_ways()
    {
        var ids = await SlugIdsAsync("heavy-pistol");
        var response = await _client.PostAsJsonAsync("/api/npcs/validate", new
        {
            name = "Strings",
            level = "Mook",
            gear = new[] { new { itemId = ids["heavy-pistol"], quantity = 1, quality = "Poor" } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var line = (await response.Content.ReadFromJsonAsync<JsonNode>())!["gearLines"]![0]!;
        line["quality"]!.GetValue<string>().Should().Be("Poor");
        line["unitPrice"]!.GetValue<int>().Should().Be(50);
        line["category"]!.GetValue<string>().Should().Be("Weapon");
    }

    [Fact]
    public async Task Manual_install_adds_the_foundational_piece_and_refuses_forbidden()
    {
        var ids = await SlugIdsAsync("kerenzikov", "neural-link");

        var ok = await _client.PostAsJsonAsync("/api/npcs/cyberware/install", new { sheet = new { name = "M", level = "Boss" }, itemId = ids["kerenzikov"] });
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var cyber = (await ok.Content.ReadFromJsonAsync<JsonNode>())!["cyberware"]!.AsArray();
        cyber.Should().HaveCount(2);
        var link = cyber.Single(c => c!["itemId"]!.GetValue<int>() == ids["neural-link"])!;
        cyber.Single(c => c!["itemId"]!.GetValue<int>() == ids["kerenzikov"])!["parentInstanceId"]!.GetValue<string>()
            .Should().Be(link["instanceId"]!.GetValue<string>());

        var refused = await _client.PostAsJsonAsync("/api/npcs/cyberware/install", new
        {
            sheet = new { name = "M", level = "Boss", cyberwareRules = new[] { new { itemId = ids["neural-link"], state = "Forbidden" } } },
            itemId = ids["kerenzikov"],
        });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("proibido");
    }

    private async Task<Dictionary<string, int>> SlugIdsAsync(params string[] slugs)
    {
        var result = new Dictionary<string, int>();
        foreach (var slug in slugs)
            result[slug] = (await _client.GetFromJsonAsync<JsonNode>($"/api/items/by-slug/{slug}"))!["item"]!["id"]!.GetValue<int>();
        return result;
    }
}
