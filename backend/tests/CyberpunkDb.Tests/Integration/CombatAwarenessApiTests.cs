using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The Solo's Combat Awareness in the fight (D3, CP:R p.146). The Streetrat Solo has Rank 4.</summary>
[Collection(ApiCollection.Name)]
public class CombatAwarenessApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task Split(int characterId, Dictionary<string, int> split)
    {
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{characterId}");
        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{characterId}/role-split", new RoleSplitRequest(sheet.Version, "Solo", split));
    }

    private async Task<EncounterDetailDto> Fight(TableScenario.Fight s) => await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");

    private async Task<string> LoadedGun(TableScenario.Fight s)
    {
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        var gun = sheet.Report.Weapons.First(w => w.Equipped && w.Magazine is not null).InstanceId;
        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/reload", new ReloadRequest(sheet.Version, gun));
        return gun;
    }

    private async Task<EncounterDetailDto> Attack(TableScenario.Fight s, string attacker, string target, string weapon, int[] damage) =>
        await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = attacker, targetIds = new[] { target }, weapon, mode = "Single", range = 5,
            attackRoll = 9, damageDice = damage,
        });

    [Fact]
    public async Task Precision_attack_3_points_adds_1()
    {
        var s = await _t.FightOnTheTable("Precisao");
        var gun = await LoadedGun(s);
        var before = (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).AttackTotal;

        await Split(s.CharacterId, new() { ["precisionAttack"] = 3 });

        (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).AttackTotal.Should().Be(before + 1);
    }

    [Fact]
    public async Task Initiative_reaction_adds_points()
    {
        var s = await _t.FightOnTheTable("Reacao");
        await Split(s.CharacterId, new() { ["initiativeReaction"] = 2 });
        var pc = (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId);

        var fight = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/initiative",
            new { version = await _t.VersionOf(s.EncounterId), rolls = new Dictionary<string, int> { [s.PcCombatantId] = 5 } });

        fight.Queue.Single(f => f.Id == s.PcCombatantId).Initiative.Should().Be(pc.Ref + 5 + 2);
    }

    [Fact]
    public async Task Spot_weakness_adds_once_per_round()
    {
        var s = await _t.FightOnTheTable("PontoFraco");
        var gun = await LoadedGun(s);
        await Split(s.CharacterId, new() { ["spotWeakness"] = 2 });
        var dice = Enumerable.Repeat(1, int.Parse((await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).Damage.Split('d')[0])).ToArray();

        var first = await Attack(s, s.PcCombatantId, s.GangerIds[0], gun, dice);
        var second = await Attack(s, s.PcCombatantId, s.GangerIds[1], gun, dice);

        first.LastAttack!.Targets.Single().Damage.Should().Be(dice.Length + 2);
        second.LastAttack!.Targets.Single().Damage.Should().Be(dice.Length);
    }

    [Fact]
    public async Task Damage_deflection_reduces_first_damage_only()
    {
        var s = await _t.FightOnTheTable("Desvio");
        await Split(s.CharacterId, new() { ["damageDeflection"] = 4 });

        var first = await Attack(s, s.GangerIds[0], s.PcCombatantId, "w0", [5, 5, 5]);
        var second = await Attack(s, s.GangerIds[1], s.PcCombatantId, "w0", [5, 5, 5]);

        first.LastAttack!.Targets.Single().Damage.Should().Be(15 - 2);
        second.LastAttack!.Targets.Single().Damage.Should().Be(15);
    }

    [Fact]
    public async Task Precision_shows_on_the_sheet_and_counts_once_in_the_fight()
    {
        var s = await _t.FightOnTheTable("FichaPrecisao");
        var gun = await LoadedGun(s);
        var before = (await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}")).Report.Weapons.Single(w => w.InstanceId == gun).AttackTotal;

        await Split(s.CharacterId, new() { ["precisionAttack"] = 3 });

        var sheet = (await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}")).Report.Weapons.Single(w => w.InstanceId == gun);
        sheet.AttackTotal.Should().Be(before + 1);
        sheet.AttackBonuses!.Should().Contain(p => p.Label == "Precision Attack" && p.Value == 1);
        (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).AttackTotal.Should().Be(sheet.AttackTotal, "the card shows the sheet's number");
    }

    [Fact]
    public async Task Awareness_notes_reach_the_chat_card()
    {
        var s = await _t.FightOnTheTable("NotasChat");
        var gun = await LoadedGun(s);
        await Split(s.CharacterId, new() { ["spotWeakness"] = 1, ["precisionAttack"] = 3 });
        var dice = Enumerable.Repeat(1, int.Parse((await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).Damage.Split('d')[0])).ToArray();

        await Attack(s, s.PcCombatantId, s.GangerIds[0], gun, dice);

        var messages = await _t.Send<List<CyberpunkDb.Application.Table.TableMessageDto>>(_t.Gm, HttpMethod.Get, "/api/table/messages");
        var card = messages.Last(m => m.Kind == CyberpunkDb.Domain.Enums.TableMessageKind.Attack).Attack!;
        card.Targets.Single().Notes.Should().Contain(n => n.StartsWith("Spot Weakness"));
        card.Notes.Should().Contain(n => n.StartsWith("Precision Attack"));
    }

    [Fact]
    public async Task Round_flags_show_used_deflection()
    {
        var s = await _t.FightOnTheTable("FlagDesvio");
        await Split(s.CharacterId, new() { ["damageDeflection"] = 4 });
        (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).RoundFlags.Should().BeEmpty();

        await Attack(s, s.GangerIds[0], s.PcCombatantId, "w0", [5, 5, 5]);

        (await Fight(s)).Queue.Single(f => f.Id == s.PcCombatantId).RoundFlags.Should().Contain("damageDeflection");
    }

    [Fact]
    public async Task Split_in_a_running_fight_is_logged_as_an_action()
    {
        var s = await _t.FightOnTheTable("Redistribui");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);

        await Split(s.CharacterId, new() { ["damageDeflection"] = 2 });

        (await Fight(s)).Log.Should().Contain(l => l.Kind == "awareness" && l.ActorId == s.PcCombatantId);
    }

    [Fact]
    public async Task Non_solo_gets_nothing()
    {
        var s = await _t.FightOnTheTable("SemSplit");
        var first = await Attack(s, s.GangerIds[0], s.PcCombatantId, "w0", [5, 5, 5]);

        first.LastAttack!.Targets.Single().Damage.Should().Be(15);
    }
}
