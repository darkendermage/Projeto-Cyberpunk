using System.Net;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The Ranger Combat Boomerang in a fight: thrown, out of hand until it comes back or is picked up (D5).</summary>
[Collection(ApiCollection.Name)]
public class BoomerangApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<(TableScenario.Fight Fight, string Boomerang)> SoloWithBoomerang(string name)
    {
        var s = await _t.FightOnTheTable(name);
        var store = await _t.Send<StorePageDto>(_t.Gm, HttpMethod.Get, "/api/characters/store?q=Ranger%20Combat%20Boomerang&page=1&pageSize=12");
        var item = store.Items.Single(i => i.Slug == "ranger-combat-boomerang");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/balance", new AdjustBalanceRequest(sheet.Version, 5000, "Teste"));
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/purchase", new PurchaseRequest(sheet.Version, item.Id, Equip: true));
        return (s, sheet.Report.Weapons.Single(w => w.ItemId == item.Id).InstanceId);
    }

    private async Task<FighterView> Pc(TableScenario.Fight s) =>
        (await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}")).Queue.Single(f => f.Id == s.PcCombatantId);

    [Fact]
    public async Task The_fight_sees_a_thrown_weapon_with_athletics()
    {
        var (s, boom) = await SoloWithBoomerang("Bumerangue");

        var weapon = (await Pc(s)).Weapon(boom)!;

        weapon.ThrownOnly.Should().BeTrue();
        weapon.Melee.Should().BeFalse();
        weapon.HalvesArmor.Should().BeTrue();
        weapon.Skill.Should().Be("athletics");
    }

    [Fact]
    public async Task Stays_out_of_hand_without_scope_until_picked_up()
    {
        var (s, boom) = await SoloWithBoomerang("BumerangueChao");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = boom,
            mode = "Single", range = 5, attackRoll = 5, damageDice = new[] { 1, 1, 1 },
        });
        (await Pc(s)).Weapon(boom)!.Thrown.Should().BeTrue();

        // A whole Round later, without a Targeting Scope, it is still on the ground.
        for (var i = 0; i < 3; i++)
            await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/next-turn", new { version = await _t.VersionOf(s.EncounterId) });
        (await Pc(s)).Weapon(boom)!.Thrown.Should().BeTrue();

        var picked = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/pick-up",
            new { version = await _t.VersionOf(s.EncounterId), weapon = boom });
        picked.Queue.Single(f => f.Id == s.PcCombatantId).Weapon(boom)!.Thrown.Should().BeFalse();
    }

    [Fact]
    public async Task Player_picks_up_only_on_own_turn()
    {
        var (s, boom) = await SoloWithBoomerang("BumeranguePega");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = boom,
            mode = "Single", range = 5, attackRoll = 5, damageDice = new[] { 1, 1, 1 },
        });
        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/pick-up", new { weapon = boom }, s.Seat.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        var view = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Post, "/api/table/play/pick-up", new { weapon = boom }, s.Seat.Token);
        view.Queue.Single(f => f.Own).Weapons!.Single(w => w.Key == boom).Thrown.Should().BeFalse();
    }
}
