using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>
/// Attacks that wait on a person (D1): the target with REF 8+ chooses to dodge (CP:R p.172) and the attacker rolls the
/// Critical Injury (CP:R p.187). The GM attacks with typed dice so the steps are predictable.
/// </summary>
[Collection(ApiCollection.Name)]
public class TablePendingAttackApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    /// <summary>A ganger shoots the PC with its heavy pistol at 5 m (DV13), interactively.</summary>
    private async Task<EncounterDetailDto> GangerShootsPc(TableScenario.Fight s, int roll = 8, int[]? damage = null) =>
        await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.GangerIds[0], targetIds = new[] { s.PcCombatantId }, weapon = "w0",
            mode = "Single", range = 5, attackRoll = roll, damageDice = damage ?? [1, 2, 3], interactive = true,
        });

    private async Task<int> PcHp(TableScenario.Fight s) =>
        (await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}")).Queue.Single(f => f.Id == s.PcCombatantId).Hp;

    [Fact]
    public async Task Ref7_target_resolves_immediately()
    {
        var s = await _t.FightOnTheTable("PendRef7");

        var detail = await GangerShootsPc(s);

        detail.Pending.Should().BeNull();
        detail.LastAttack.Should().NotBeNull();
    }

    [Fact]
    public async Task Ref8_target_pauses_on_defense()
    {
        var s = await _t.FightOnTheTable("PendRef8", row: 2);
        var hp = await PcHp(s);

        var detail = await GangerShootsPc(s);

        detail.Pending!.Step.Should().Be(PendingStep.Defense);
        detail.Pending.TargetId.Should().Be(s.PcCombatantId);
        detail.Pending.RangeDv.Should().Be(13);
        detail.LastAttack.Should().BeNull();
        (await PcHp(s)).Should().Be(hp);
    }

    [Fact]
    public async Task Dodge_uses_evasion_roll_as_dv()
    {
        var s = await _t.FightOnTheTable("PendEsquiva", row: 2);
        var pending = (await GangerShootsPc(s)).Pending!;

        var detail = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/pending/defense",
            new { version = await _t.VersionOf(s.EncounterId), targetId = pending.TargetId, dodge = true });

        detail.Pending.Should().BeNull();
        var target = detail.LastAttack!.Targets.Single();
        target.DefenseRoll.Should().NotBeNull();
        target.Dv.Should().Be(pending.Evasion + target.DefenseRoll);
    }

    [Fact]
    public async Task No_dodge_uses_range_dv()
    {
        var s = await _t.FightOnTheTable("PendSemEsquiva", row: 2);
        var pending = (await GangerShootsPc(s)).Pending!;

        var detail = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/pending/defense",
            new { version = await _t.VersionOf(s.EncounterId), targetId = pending.TargetId, dodge = false });

        var target = detail.LastAttack!.Targets.Single();
        target.DefenseRoll.Should().BeNull();
        target.Dv.Should().Be(13);
    }

    [Fact]
    public async Task Two_sixes_pause_on_critical_and_the_roll_applies_the_injury()
    {
        var s = await _t.FightOnTheTable("PendCritico");

        // Ganger attack 12 + 9 = 21 against DV13: a hit with two 6s.
        var paused = await GangerShootsPc(s, roll: 9, damage: [6, 6, 1]);
        paused.Pending!.Step.Should().Be(PendingStep.CriticalInjury);

        var detail = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/pending/critical",
            new { version = await _t.VersionOf(s.EncounterId), targetId = s.PcCombatantId });

        detail.Pending.Should().BeNull();
        detail.LastAttack!.Targets.Single().CriticalInjury.Should().NotBeNull();
        detail.LastAttack.CritDice.Should().HaveCount(2);
    }

    [Fact]
    public async Task Cancel_spends_nothing_and_clears()
    {
        var s = await _t.FightOnTheTable("PendCancela", gangerRef: 8);
        var fight = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        var gun = fight.Queue.Single(f => f.Id == s.PcCombatantId).Weapons.First(w => w.Magazine is not null).Key;
        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/reload",
            new ReloadRequest(sheet.Version, gun));
        var rounds = (await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}")).Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).Rounds;

        var paused = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = gun,
            mode = "Single", range = 5, attackRoll = 5, interactive = true,
        });
        paused.Pending!.Step.Should().Be(PendingStep.Defense, "the ganger has REF 8");

        var detail = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/pending/cancel", new { version = await _t.VersionOf(s.EncounterId) });

        detail.Pending.Should().BeNull();
        detail.Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).Rounds.Should().Be(rounds);
    }

    [Fact]
    public async Task Other_actions_refused_while_pending()
    {
        var s = await _t.FightOnTheTable("PendBloqueia", row: 2);
        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);
        await GangerShootsPc(s);

        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/next-turn", new { version = await _t.VersionOf(s.EncounterId) }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/damage",
            new { version = await _t.VersionOf(s.EncounterId), amount = 3 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Undo_after_pending_attack_restores()
    {
        var s = await _t.FightOnTheTable("PendDesfaz", row: 2);
        var hp = await PcHp(s);
        await GangerShootsPc(s, roll: 9, damage: [5, 5, 5]);
        var done = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/pending/defense",
            new { version = await _t.VersionOf(s.EncounterId), targetId = s.PcCombatantId, dodge = false });
        done.LastAttack!.Targets.Single().Hit.Should().BeTrue("21 beats DV13");

        await _t.Send<JsonElement>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/undo-attack", new { version = await _t.VersionOf(s.EncounterId) });

        (await PcHp(s)).Should().Be(hp);
    }

    [Fact]
    public async Task Player_target_answers_own_defense_and_sees_it_as_theirs()
    {
        var s = await _t.FightOnTheTable("PendJogador", row: 2);
        await GangerShootsPc(s);

        var view = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Get, "/api/table/play/encounter", seat: s.Seat.Token);
        view.PendingMine.Should().BeTrue();
        view.Pending!.Step.Should().Be(PendingStep.Defense);

        var after = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Post, "/api/table/play/defense", new { dodge = false }, s.Seat.Token);
        after.Pending.Should().BeNull();
    }

    [Fact]
    public async Task Player_cannot_answer_for_another_pc()
    {
        var s = await _t.FightOnTheTable("PendAlheio", row: 2);
        var other = await _t.FinalizedSolo("Intrometido");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Intrometido", other.Id), expected: HttpStatusCode.Created);
        var e = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Ally", characterId = other.Id });
        await GangerShootsPc(s);

        var view = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Get, "/api/table/play/encounter", seat: seat.Token);
        view.PendingMine.Should().BeFalse();
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/defense", new { dodge = true }, seat.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Attacker_player_rolls_critical()
    {
        var s = await _t.FightOnTheTable("PendCritJogador");
        var fight = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        var gun = fight.Queue.Single(f => f.Id == s.PcCombatantId).Weapons.First(w => w.Magazine is not null).Key;
        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/reload",
            new ReloadRequest(sheet.Version, gun));
        var damageDice = fight.Queue.Single(f => f.Id == s.PcCombatantId).Weapons.Single(w => w.Key == gun).Damage.Split('d')[0];
        var sixes = Enumerable.Repeat(6, int.Parse(damageDice)).ToArray();

        var paused = await _t.Send<EncounterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack", new
        {
            version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = gun,
            mode = "Single", range = 5, attackRoll = 9, damageDice = sixes, interactive = true,
        });
        paused.Pending!.Step.Should().Be(PendingStep.CriticalInjury);
        (await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Get, "/api/table/play/encounter", seat: s.Seat.Token)).PendingMine.Should().BeTrue();

        var after = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Post, "/api/table/play/critical", null, s.Seat.Token);

        after.Pending.Should().BeNull();
    }

    [Fact]
    public async Task Pending_view_hides_attack_total()
    {
        var s = await _t.FightOnTheTable("PendSemTotal", row: 2);
        await GangerShootsPc(s);

        var raw = await _t.Raw(_t.Player, HttpMethod.Get, "/api/table/play/encounter", seat: s.Seat.Token);
        var pending = JsonNode.Parse(await raw.Content.ReadAsStringAsync())!["pending"]!.AsObject();

        pending.ContainsKey("attackTotal").Should().BeFalse();
        pending.ContainsKey("attackRoll").Should().BeFalse();
    }
}
