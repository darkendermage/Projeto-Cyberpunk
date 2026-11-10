using System.Net;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// weapon-rules.json through the API: free modes picked on the attack, modes switched on the weapon, extra magazines
[Collection(ApiCollection.Name)]
public class WeaponModesApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<(TableScenario.Fight Fight, string Weapon)> PcWith(string name, string query, string slug)
    {
        var s = await _t.FightOnTheTable(name);
        var store = await _t.Send<StorePageDto>(_t.Gm, HttpMethod.Get, $"/api/characters/store?q={Uri.EscapeDataString(query)}&page=1&pageSize=12");
        var item = store.Items.Single(i => i.Slug == slug);
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/balance", new AdjustBalanceRequest(sheet.Version, 10_000, "Teste"));
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/purchase", new PurchaseRequest(sheet.Version, item.Id, Equip: true));
        return (s, sheet.Report.Weapons.Single(w => w.ItemId == item.Id).InstanceId);
    }

    private async Task<WeaponView> Weapon(TableScenario.Fight s, string key) =>
        (await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}")).Queue.Single(f => f.Id == s.PcCombatantId).Weapon(key)!;

    [Fact]
    public async Task Crowd_buster_shrieker_spends_its_battery_and_reloads_it_apart()
    {
        var (s, buster) = await PcWith("Shrieker", "Crowd Buster", "pursuit-security-crowd-buster");
        var weapon = await Weapon(s, buster);
        weapon.Modes!.Select(m => m.ModeId).Should().Equal("rifle", "shrieker");
        weapon.Magazines!.Should().Contain(m => m.Id == "battery" && m.Rounds == 8);

        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        var after = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = buster,
            weaponMode = "shrieker", mode = "Single", range = 5, attackRoll = 9,
        });
        after.LastAttack!.Targets.Single().Damage.Should().Be(0, "the shrieker forces a check instead of dealing damage");
        (await Weapon(s, buster)).Magazines!.Single(m => m.Id == "battery").Rounds.Should().Be(7);

        var reloaded = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/reload",
            new { version = await _t.VersionOf(s.EncounterId), weapon = buster, magazine = "battery" });
        reloaded.Queue.Single(f => f.Id == s.PcCombatantId).Weapon(buster)!.Magazines!.Single(m => m.Id == "battery").Rounds.Should().Be(8);
    }

    [Fact]
    public async Task Switched_mode_goes_on_the_sheet_and_the_player_switches_only_on_its_turn()
    {
        var (s, cutie) = await PcWith("Hypurr", "MicroCutie", "micro-cutie-sanroo");
        (await Weapon(s, cutie)).ModeId.Should().Be("normal");

        var switched = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/mode",
            new { version = await _t.VersionOf(s.EncounterId), weapon = cutie, mode = "hypurrburst" });
        switched.Queue.Single(f => f.Id == s.PcCombatantId).Weapon(cutie)!.ModeId.Should().Be("hypurrburst");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        sheet.Report.Weapons.Single(w => w.InstanceId == cutie).Mode.Should().Be("hypurrburst");

        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/mode", new { weapon = cutie, mode = "normal" }, s.Seat.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        var mine = await _t.Send<PlayerReloadResult>(_t.Player, HttpMethod.Post, "/api/table/play/mode", new { weapon = cutie, mode = "normal" }, s.Seat.Token);
        mine.Encounter!.Queue.Single(f => f.Own).Weapons!.Single(w => w.Key == cutie).ModeId.Should().Be("normal");
    }

    [Fact]
    public async Task Sheet_switches_modes_and_refuses_free_ones()
    {
        var (s, cutie) = await PcWith("FichaModo", "MicroCutie", "micro-cutie-sanroo");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");

        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/mode", new WeaponModeRequest(sheet.Version, cutie, "hypurrburst"));
        sheet.Report.Weapons.Single(w => w.InstanceId == cutie).SwitchModes!.Select(m => m.Id).Should().Equal("normal", "hypurrburst");
        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/mode", new WeaponModeRequest(sheet.Version, cutie, "nope")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/repair", new WeaponRepairRequest(sheet.Version, cutie));
        sheet.Report.Weapons.Single(w => w.InstanceId == cutie).Destroyed.Should().BeFalse();
    }
}
