using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class NpcCompendiumApiTests(ApiFactory factory)
{
    private async Task<(HttpStatusCode Status, string Body)> Get(string url, string language = "en-US")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_has_sheets_and_profiles()
    {
        var (status, body) = await Get("/api/npc-compendium");
        status.Should().Be(HttpStatusCode.OK);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();

        string Kind(string id) => items.Single(i => i.GetProperty("id").GetString() == id).GetProperty("kind").GetString()!;
        Kind("adam-smasher").Should().Be("Sheet");
        Kind("morgan-blackhand").Should().Be("Profile");

        var names = items.Select(i => i.GetProperty("name").GetString()!).ToList();
        names.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detail_returns_the_printed_sheet()
    {
        var (status, body) = await Get("/api/npc-compendium/adam-smasher");
        status.Should().Be(HttpStatusCode.OK);
        var sheet = JsonDocument.Parse(body).RootElement.GetProperty("sheet");
        sheet.GetProperty("hp").GetInt32().Should().Be(75);
        sheet.GetProperty("deathSave").GetInt32().Should().Be(17);
        sheet.GetProperty("stats").GetProperty("BODY").GetInt32().Should().Be(17);
        sheet.GetProperty("stats").GetProperty("EMP").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Texts_follow_the_language()
    {
        string Summary(string body) => JsonDocument.Parse(body).RootElement.GetProperty("summary").GetString()!;
        var en = Summary((await Get("/api/npc-compendium/adam-smasher", "en-US")).Body);
        var pt = Summary((await Get("/api/npc-compendium/adam-smasher", "pt-BR")).Body);
        en.Should().NotBeEmpty();
        en.Should().NotBe(pt);
    }

    [Fact]
    public async Task Unknown_id_is_404()
    {
        var (status, body) = await Get("/api/npc-compendium/nobody");
        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("isn't in the compendium");
    }

    // ------------------------------------------------------------------ joining a fight

    private async Task<(HttpStatusCode Status, JsonNode Body)> PostJson(string url, object body, string language = "pt-BR")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.PostAsJsonAsync(url, body);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<JsonNode>())!);
    }

    private async Task<JsonNode> NewFight()
    {
        var (_, e) = await PostJson("/api/encounters", new { name = "Compêndio" });
        return e;
    }

    private static int V(JsonNode n) => n["version"]!.GetValue<int>();
    private static JsonNode Fighter(JsonNode n, string name) => n["queue"]!.AsArray().First(f => f!["name"]!.GetValue<string>().StartsWith(name))!;

    private async Task<(JsonNode Fight, string SmasherId, string TargetId)> SmasherAndTarget()
    {
        var e = await NewFight();
        var id = e["id"]!.GetValue<int>();
        (_, e) = await PostJson($"/api/encounters/{id}/combatants", new { version = V(e), side = "Enemy", compendiumNpcId = "adam-smasher" });
        (_, e) = await PostJson($"/api/encounters/{id}/combatants", new
        {
            version = V(e), side = "Ally", generic = new { name = "Alvo", hp = 200, spBody = 0, spHead = 0, evasionBase = 1 },
        });
        return (e, Fighter(e, "Adam")["id"]!.GetValue<string>(), Fighter(e, "Alvo")["id"]!.GetValue<string>());
    }

    private async Task<JsonNode> Attack(JsonNode fight, string attackerId, string targetId, string weapon, object? extra = null)
    {
        var body = new JsonObject
        {
            ["version"] = V(fight), ["attackerId"] = attackerId, ["targetIds"] = new JsonArray(targetId), ["weapon"] = weapon,
        };
        foreach (var (k, v) in JsonSerializer.SerializeToNode(extra ?? new { })!.AsObject()) body[k] = v?.DeepClone();
        var (status, result) = await PostJson($"/api/encounters/{fight["id"]!.GetValue<int>()}/attack", body);
        status.Should().Be(HttpStatusCode.OK, result.ToJsonString());
        return result;
    }

    [Fact]
    public async Task Adam_Smasher_joins_a_fight()
    {
        var (e, smasherId, _) = await SmasherAndTarget();
        var smasher = Fighter(e, "Adam");
        smasher["name"]!.GetValue<string>().Should().Be("Adam Smasher");
        smasher["kind"]!.GetValue<string>().Should().Be("Npc");
        smasher["maxHp"]!.GetValue<int>().Should().Be(75);
        smasher["spBody"]!.GetValue<int>().Should().Be(18);
        smasher["source"]!.GetValue<string>().Should().Contain("p.");
        var weapons = smasher["weapons"]!.AsArray().Select(w => w!["name"]!.GetValue<string>()).ToList();
        weapons.Should().Contain(["Shoulder-Mounted Rocket Launcher", "Pop-Up Autoshotgun", "Adam Smasher's Chain Gun"]);
        smasherId.Should().NotBeNull();
    }

    [Fact]
    public async Task Count_numbers_the_copies()
    {
        var e = await NewFight();
        (_, e) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants",
            new { version = V(e), side = "Enemy", compendiumNpcId = "falco", count = 2 });
        var names = e["queue"]!.AsArray().Select(f => f!["name"]!.GetValue<string>()).ToList();
        names.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        names.Should().OnlyContain(n => n.StartsWith("Falco"));
    }

    [Fact]
    public async Task A_sheet_that_prints_luck_joins_a_fight()
    {
        var (_, detail) = await Get("/api/npc-compendium/mover");
        JsonNode.Parse(detail)!["sheet"]!["stats"]!["LUCK"]!.GetValue<int>().Should().Be(6);

        var e = await NewFight();
        var (status, fight) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants",
            new { version = V(e), side = "Enemy", compendiumNpcId = "mover" });
        status.Should().Be(HttpStatusCode.OK);
        var mover = Fighter(fight, "Mover");
        mover["hp"]!.GetValue<int>().Should().Be(45);
        mover["spBody"]!.GetValue<int>().Should().Be(11);
    }

    [Fact]
    public async Task A_sheet_from_a_plus_supplement_joins_a_fight()
    {
        var (_, detail) = await Get("/api/npc-compendium/veronica-stiles");
        var npc = JsonNode.Parse(detail)!;
        npc["kind"]!.GetValue<string>().Should().Be("Sheet");
        npc["sources"]!.AsArray().Select(s => s!["book"]!.GetValue<string>()).Should().Contain("Tales of the RED+");

        var e = await NewFight();
        var (status, fight) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants",
            new { version = V(e), side = "Enemy", compendiumNpcId = "veronica-stiles" });
        status.Should().Be(HttpStatusCode.OK);
        var stiles = Fighter(fight, "Veronica Stiles");
        stiles["hp"]!.GetValue<int>().Should().Be(45);
        stiles["spBody"]!.GetValue<int>().Should().Be(11);
    }

    [Fact]
    public async Task A_profile_cannot_join()
    {
        var e = await NewFight();
        var (status, body) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants",
            new { version = V(e), side = "Enemy", compendiumNpcId = "morgan-blackhand" });
        status.Should().Be(HttpStatusCode.BadRequest);
        body["errors"]!["compendiumNpcId"]![0]!.GetValue<string>().Should().Be("Este NPC não tem ficha oficial.");
    }

    [Fact]
    public async Task Unknown_npc_is_404()
    {
        var e = await NewFight();
        var (status, _) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants",
            new { version = V(e), side = "Enemy", compendiumNpcId = "nobody" });
        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_compendium_npc_can_attack()
    {
        var (e, smasher, target) = await SmasherAndTarget();
        var weapons = Fighter(e, "Adam")["weapons"]!.AsArray();
        var chain = weapons.Single(w => w!["name"]!.GetValue<string>().Contains("Chain Gun"))!;
        var rocket = weapons.Single(w => w!["name"]!.GetValue<string>().Contains("Rocket"))!;
        var shotgun = weapons.Single(w => w!["name"]!.GetValue<string>().Contains("Autoshotgun"))!;

        // Melee: the fists use the printed attack base.
        var fists = weapons[0]!;
        var hit = await Attack(e, smasher, target, fists["key"]!.GetValue<string>(), new { attackRoll = 5, damageDice = new[] { 3, 3, 3, 3, 3 } });
        hit["lastAttack"]!["attackTotal"]!.GetValue<int>().Should().Be(fists["attackTotal"]!.GetValue<int>() + 5);
        hit["lastAttack"]!["targets"]![0]!["hit"]!.GetValue<bool>().Should().BeTrue();

        // Ranged weapons need a class the range table knows.
        foreach (var w in new[] { rocket, shotgun })
        {
            var shot = await Attack(hit, smasher, target, w["key"]!.GetValue<string>(), new { range = 5, attackRoll = 10 });
            shot["lastAttack"]!["targets"]![0]!["hit"]!.GetValue<bool>().Should().BeTrue(w["name"]!.GetValue<string>());
            hit = shot;
        }

        // The Chain Gun's burst uses the sheet's own table (DV22 at 0-6 m), the x5 multiplier and 10 rounds of its 80.
        chain["magazine"]!.GetValue<int>().Should().Be(80);
        var burst = await Attack(hit, smasher, target, chain["key"]!.GetValue<string>(),
            new { mode = "Autofire", range = 5, attackRoll = 10, damageDice = new[] { 3, 4 } });
        var result = burst["lastAttack"]!["targets"]![0]!;
        result["hit"]!.GetValue<bool>().Should().BeTrue();
        result["dv"]!.GetValue<int>().Should().Be(22);
        result["damage"]!.GetValue<int>().Should().Be(35, "7 on the dice, times the x5 cap");
        Fighter(burst, "Adam")["weapons"]!.AsArray().Single(w => w!["name"]!.GetValue<string>().Contains("Chain Gun"))!["rounds"]!.GetValue<int>().Should().Be(70);
    }

    // ------------------------------------------------------------------ printed rules in combat

    private async Task<JsonNode> FightWith(string compendiumNpcId)
    {
        var e = await NewFight();
        (_, e) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants", new { version = V(e), side = "Enemy", compendiumNpcId });
        return e;
    }

    private async Task<JsonNode> WithBlade(JsonNode fight)
    {
        var (_, e) = await PostJson($"/api/encounters/{fight["id"]!.GetValue<int>()}/combatants", new
        {
            version = V(fight), side = "Ally", generic = new { name = "Lâmina", weaponClass = "VeryHeavyMelee", damage = "5d6", attackBase = 40 },
        });
        return e;
    }

    private static JsonNode WeaponNamed(JsonNode fighter, string name) =>
        fighter["weapons"]!.AsArray().First(w => w!["name"]!.GetValue<string>() == name)!;

    [Fact]
    public async Task An_assault_rifle_from_the_table_fires_autofire()
    {
        var e = await FightWith("coldfire");
        (_, e) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/combatants", new
        {
            version = V(e), side = "Ally", generic = new { name = "Alvo", hp = 200, spBody = 0, spHead = 0, evasionBase = 1 },
        });
        var coldfire = Fighter(e, "Coldfire");
        var rifle = WeaponNamed(coldfire, "Arasaka WAA Bullpup Assault Weapon");
        rifle["autofireMultiplier"]!.GetValue<int>().Should().Be(4);
        rifle["autofireTotal"]!.GetValue<int>().Should().Be(12, "the printed Autofire 14 (12), with the armor penalty");

        var burst = await Attack(e, coldfire["id"]!.GetValue<string>(), Fighter(e, "Alvo")["id"]!.GetValue<string>(), rifle["key"]!.GetValue<string>(),
            new { mode = "Autofire", range = 15, attackRoll = 10, damageDice = new[] { 3, 4 } });

        var result = burst["lastAttack"]!["targets"]![0]!;
        result["dv"]!.GetValue<int>().Should().Be(17, "Assault Rifle at 13-25 m on the Autofire table (CP:R p.173)");
        result["damage"]!.GetValue<int>().Should().Be(28, "7 on the dice, times the x4 cap of a margin of 5");
    }

    [Fact]
    public async Task Printed_brawling_keeps_full_armor_unless_the_sheet_says_otherwise()
    {
        WeaponNamed(Fighter(await FightWith("falco"), "Falco"), "Brawling Attack")["halvesArmor"]!.GetValue<bool>().Should().BeFalse("CP:R p.176");
        WeaponNamed(Fighter(await FightWith("adam-smasher"), "Adam"), "Adam Smasher's Fists")["halvesArmor"]!.GetValue<bool>().Should().BeTrue("the sheet prints Halves SP");
    }

    [Fact]
    public async Task A_combat_jaw_is_a_melee_weapon()
    {
        var gustav = Fighter(await FightWith("gustav"), "Gustav");
        var jaw = WeaponNamed(gustav, "Combat Jaw");
        jaw["halvesArmor"]!.GetValue<bool>().Should().BeTrue("Very Heavy Melee Weapon (Danger Gal Dossier p.145, CP:R p.176)");
        jaw["skill"]!.GetValue<string>().Should().Be("melee-weapon");
        jaw["attackTotal"]!.GetValue<int>().Should().Be(12, "the printed Melee Weapon Skill Base");
        WeaponNamed(gustav, "Brawling Attack")["halvesArmor"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Smasher_plating_keeps_its_printed_sp_against_melee_as_it_ablates()
    {
        var e = await WithBlade(await FightWith("adam-smasher"));
        var smasher = Fighter(e, "Adam")["id"]!.GetValue<string>();
        var blade = Fighter(e, "Lâmina");
        blade["weapons"]![0]!["halvesArmor"]!.GetValue<bool>().Should().BeTrue();
        var swing = new { attackRoll = 5, defenseRolls = new Dictionary<string, int> { [smasher] = 1 }, damageDice = new[] { 5, 5, 5, 5, 5 } };

        var first = await Attack(e, blade["id"]!.GetValue<string>(), smasher, "w0", swing);
        first["lastAttack"]!["targets"]![0]!["through"]!.GetValue<int>().Should().Be(7, "25 through SP 18");

        var second = await Attack(first, blade["id"]!.GetValue<string>(), smasher, "w0", swing);
        second["lastAttack"]!["targets"]![0]!["through"]!.GetValue<int>().Should().Be(8, "25 through SP 17: still not halved after ablation");
    }

    [Fact]
    public async Task Ordinary_armor_still_halves_against_melee()
    {
        var e = await WithBlade(await FightWith("falco"));
        var falco = Fighter(e, "Falco")["id"]!.GetValue<string>();
        var hit = await Attack(e, Fighter(e, "Lâmina")["id"]!.GetValue<string>(), falco, "w0",
            new { attackRoll = 5, defenseRolls = new Dictionary<string, int> { [falco] = 1 }, damageDice = new[] { 5, 5, 5, 5, 5 } });
        hit["lastAttack"]!["targets"]![0]!["through"]!.GetValue<int>().Should().Be(19, "25 through half of SP 11, rounded up");
    }

    [Fact]
    public async Task A_dash_on_the_printed_table_is_out_of_range_on_the_sheet()
    {
        var (e, smasher, target) = await SmasherAndTarget();
        var shotgun = Fighter(e, "Adam")["weapons"]!.AsArray().Single(w => w!["name"]!.GetValue<string>().Contains("Autoshotgun"))!;
        var (status, body) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/attack", new
        {
            version = V(e), attackerId = smasher, targetIds = new[] { target }, weapon = shotgun["key"]!.GetValue<string>(), range = 300, attackRoll = 5,
        });
        status.Should().Be(HttpStatusCode.BadRequest);
        body["errors"]!["range"]![0]!.GetValue<string>().Should().Be("Pop-Up Autoshotgun não alcança 300 m: fora do alcance na tabela da própria ficha.");
    }

    [Fact]
    public async Task Null_stats_stay_out_of_the_snapshot()
    {
        var (e, _, _) = await SmasherAndTarget();
        var stats = Fighter(e, "Adam")["stats"]!.AsArray().Select(s => s!["id"]!.GetValue<string>()).ToList();
        stats.Should().NotContain("EMP").And.Contain("BODY");
        (_, e) = await PostJson($"/api/encounters/{e["id"]!.GetValue<int>()}/initiative", new { version = V(e) });
        Fighter(e, "Adam")["initiative"]!.GetValue<int>().Should().BeGreaterThan(0);
    }
}
