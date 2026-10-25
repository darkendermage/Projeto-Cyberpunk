using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class EncountersApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonNode> Post(string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await _client.PostAsJsonAsync(url, body, Json);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    private async Task<CharacterDetailDto> Character(HttpMethod method, string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: Json) };
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>(Json))!;
    }

    /// <summary>A finalized Streetrat Solo, as in CharactersApiTests.</summary>
    private async Task<CharacterDetailDto> FinalizedSolo()
    {
        var c = await Character(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = "Alvo", Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            HttpStatusCode.Created);
        c = await Character(HttpMethod.Post, $"/api/characters/{c.Id}/creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Character(HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with
        {
            Creation = c.Sheet.Creation with { StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 } },
        }, null));
        return await Character(HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
    }

    private static int V(JsonNode n) => n["version"]!.GetValue<int>();
    private static JsonNode Fighter(JsonNode n, string name) => n["queue"]!.AsArray().First(f => f!["name"]!.GetValue<string>().StartsWith(name))!;

    [Fact]
    public async Task Rules_bring_the_range_table_and_the_character_combat_rules()
    {
        var rules = (await _client.GetFromJsonAsync<JsonNode>("/api/encounters/rules"))!;
        rules["rules"]!["singleShot"]!["source"]!.GetValue<string>().Should().Contain("CP:R p.173");
        rules["characterCombat"]!["combat"]!["criticalInjuries"].Should().NotBeNull();
    }

    [Fact]
    public async Task A_fight_with_a_pc_generated_gangers_cover_and_the_log()
    {
        var pc = await FinalizedSolo();
        var hpBefore = pc.Report.Health.CurrentHp;

        var e = await Post("/api/encounters", new { name = "Emboscada" }, HttpStatusCode.Created);
        var id = e["id"]!.GetValue<int>();
        e = await Post($"/api/encounters/{id}/combatants", new { version = V(e), side = "Ally", characterId = pc.Id });
        e = await Post($"/api/encounters/{id}/combatants", new
        {
            version = V(e), side = "Enemy", count = 3, generate = new { level = "Mook", gangId = "maelstrom", seed = 9 },
        });
        e = await Post($"/api/encounters/{id}/combatants", new
        {
            version = V(e), side = "Enemy", generic = new { name = "Atirador", attackBase = 14, damage = "3d6", weaponClass = "HeavyPistol" },
        });
        e["queue"]!.AsArray().Should().HaveCount(5);

        e = await Post($"/api/encounters/{id}/initiative", new { version = V(e) });
        e["activeId"]!.GetValue<string>().Should().NotBeNull();
        e["queue"]!.AsArray().Select(f => f!["initiative"]!.GetValue<int>()).Should().BeInDescendingOrder();

        // A generic gunman shoots the PC: 25 damage to the body goes to the character sheet.
        var shooter = Fighter(e, "Atirador");
        var target = Fighter(e, "Alvo");
        e = await Post($"/api/encounters/{id}/attack", new
        {
            version = V(e), attackerId = shooter["id"]!.GetValue<string>(), targetIds = new[] { target["id"]!.GetValue<string>() },
            weapon = "w0", range = 5, attackRoll = 10, damageDice = new[] { 5, 5, 5 },
        });
        e["lastAttack"]!["targets"]![0]!["hit"]!.GetValue<bool>().Should().BeTrue();
        var sheet = (await _client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{pc.Id}", Json))!;
        sheet.Report.Health.CurrentHp.Should().BeLessThan(hpBefore, "o dano no PC vai para a ficha");

        // Thick concrete cover (CP:R p.182): 17 damage leaves 8 HP, the next hit destroys it.
        e = await Post($"/api/encounters/{id}/covers", new { version = V(e), label = "Barricada", material = "concrete", thick = true });
        var cover = e["covers"]![0]!["id"]!.GetValue<string>();
        var ganger = Fighter(e, e["queue"]!.AsArray().First(f => f!["kind"]!.GetValue<string>() == "Npc")!["name"]!.GetValue<string>());
        var gangerId = ganger["id"]!.GetValue<string>();
        e = await Post($"/api/encounters/{id}/combatants/{gangerId}/cover", new { version = V(e), coverId = cover });
        e = await Post($"/api/encounters/{id}/covers/{cover}/damage", new { version = V(e), amount = 17 });
        e["covers"]![0]!["hp"]!.GetValue<int>().Should().Be(8);
        e = await Post($"/api/encounters/{id}/covers/{cover}/damage", new { version = V(e), amount = 17 });
        e["covers"]![0]!["hp"]!.GetValue<int>().Should().Be(0);
        e["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == gangerId)!["coverId"].Should().BeNull();

        // Taking a ganger down to Mortally Wounded and failing a Death Save with a natural 10.
        e = await Post($"/api/encounters/{id}/combatants/{gangerId}/damage", new { version = V(e), amount = 200, bypassArmor = true });
        Fighter(e, ganger["name"]!.GetValue<string>())["mustDeathSave"]!.GetValue<bool>().Should().BeTrue();
        e = await Post($"/api/encounters/{id}/combatants/{gangerId}/death-save", new { version = V(e), roll = 10 });
        Fighter(e, ganger["name"]!.GetValue<string>())["dead"]!.GetValue<bool>().Should().BeTrue();

        e = await Post($"/api/encounters/{id}/next-turn", new { version = V(e) });
        var stale = await _client.PostAsJsonAsync($"/api/encounters/{id}/next-turn", new { version = V(e) - 1 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        e["log"]!.AsArray().Select(l => l!["text"]!.GetValue<string>()).Should().Contain(t => t.Contains("ataca com"));
        e = await Post($"/api/encounters/{id}/end", new { version = V(e) });
        e["status"]!.GetValue<string>().Should().Be("Ended");
        (await _client.DeleteAsync($"/api/encounters/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
