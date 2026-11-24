using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// Grenades that reload launchers, Skill Chips made for a skill, Critical Injuries by hand, NPC cyberweapons
[Collection(ApiCollection.Name)]
public class Round8ApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<int> ItemId(string slug) =>
        (await _t.Gm.GetFromJsonAsync<JsonNode>($"/api/items/by-slug/{slug}"))!["item"]!["id"]!.GetValue<int>();

    private async Task<CharacterDetailDto> Rich(string name)
    {
        var c = await _t.FinalizedSolo(name);
        return await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/balance", new AdjustBalanceRequest(c.Version, 50_000, "teste"));
    }

    private Task<CharacterDetailDto> Buy(CharacterDetailDto c, int itemId, int quantity = 1, string? skillKey = null, bool? install = null) =>
        _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/purchase",
            new PurchaseRequest(c.Version, itemId, quantity, Install: install, Acknowledge: true, SkillKey: skillKey));

    [Fact]
    public async Task Grenades_reload_a_grenade_launcher_like_the_grenade_ammo()
    {
        var c = await Rich("Granadeiro");
        c = await Buy(c, await ItemId("grenade-launcher-padrao"));
        c = await Buy(c, await ItemId("flashbang-grenade"), 3);
        var launcher = c.Report.Weapons.Single(w => w.Slug == "grenade-launcher-padrao");

        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/weapons/reload", new ReloadRequest(c.Version, launcher.InstanceId));

        c.Report.Weapons.Single(w => w.InstanceId == launcher.InstanceId).LoadedRounds.Should().Be(2, "the launcher holds 2 (CP:R p.341)");
        c.Report.Inventory.Single(i => i.Slug == "flashbang-grenade").Rounds.Should().Be(1);
    }

    [Fact]
    public async Task A_Skill_Chip_is_bought_for_a_skill_costs_by_it_and_trains_it_at_3()
    {
        var c = await Rich("Chipado");
        var before = c.Sheet.Wallet.Balance;
        var chip = await ItemId("skill-chip");
        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/purchase", new PurchaseRequest(c.Version, chip, Install: true, Acknowledge: true)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a chip is made for one skill");

        var level = c.Report.Skills.Single(s => s.Key == "drive-land-vehicle").Level;
        c = await Buy(c, chip, skillKey: "drive-land-vehicle", install: true);

        var line = c.Report.Cyberware.Single(l => l.Slug == "skill-chip");
        line.Name.Should().Be("Skill Chip (Drive Land Vehicle)");
        var skill = c.Report.Skills.Single(s => s.Key == "drive-land-vehicle");
        if (level < 3) skill.ModifierParts.Should().Contain(p => p.Label.StartsWith("Skill Chip") && p.Value == 3 - level);
        c.Sheet.Cyberware.Single(x => x.ItemId == chip).PricePaid.Should().Be(500, "chips for skills that aren't (x2) cost 500eb (CP:R p.358)");
        (before - c.Sheet.Wallet.Balance).Should().BeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task A_Critical_Injury_can_be_applied_by_hand_without_the_bonus_damage()
    {
        var s = await _t.FightOnTheTable("Flashbang", gangers: 1);
        var e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        var ganger = s.GangerIds[0];
        var hp = e!["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == ganger)!["hp"]!.GetValue<int>();

        e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{ganger}/injury",
            new CombatantInjuryRequest(await _t.VersionOf(s.EncounterId), InjuryLocation.Head, "Damaged Eye", BonusDamage: false));
        var fighter = e!["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == ganger)!;
        fighter["hp"]!.GetValue<int>().Should().Be(hp);
        fighter["injuries"]!.AsArray().Select(i => i!["name"]!.GetValue<string>()).Should().Contain("Damaged Eye");

        e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/injury",
            new CombatantInjuryRequest(await _t.VersionOf(s.EncounterId), InjuryLocation.Head, "Damaged Ear", BonusDamage: false));
        var pc = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        pc.Sheet.Injuries.Should().Contain(i => i.Name == "Damaged Ear");
    }

    [Fact]
    public async Task A_generated_NPC_with_Wolvers_attacks_with_them()
    {
        var wolvers = await ItemId("wolvers");
        var e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, "/api/encounters", new { name = "Garras" }, expected: HttpStatusCode.Created);
        var id = e!["id"]!.GetValue<int>();
        e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new
        {
            version = e["version"]!.GetValue<int>(), side = "Enemy",
            generate = new { level = "Lieutenant", seed = 4, @base = new { cyberwareRules = new[] { new { itemId = wolvers, state = "Required" } } } },
        });
        var weapons = e!["queue"]!.AsArray()[0]!["weapons"]!.AsArray().Select(w => w!["name"]!.GetValue<string>()).ToList();
        weapons.Should().Contain("Wolvers");
    }
}
