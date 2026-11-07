using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters;

/// <summary>
/// Attacks that wait on a person: a target with REF 8+ chooses whether to dodge a shot (CP:R p.172: their Evasion roll
/// becomes the DV), and the attacker rolls the Critical Injury when two or more damage dice come up 6 (CP:R p.187).
/// Each step fixes one more die in the stored request; once nothing is missing the attack is applied as usual.
/// </summary>
public sealed partial class EncounterService
{
    /// <summary>
    /// Fixes the attack's dice step by step and stops at the first decision someone must take; applies the attack when
    /// none is left. <paramref name="decided"/> lists the targets whose dodge choice is made.
    /// </summary>
    private async Task<EncounterState> ContinueAttackAsync(EncounterState s, Encounter e, List<string> notices, Dictionary<string, FighterView> fighters,
        AttackRequest r, List<string> decided, CancellationToken ct)
    {
        var (attacker, targets) = Participants(fighters, r);
        var weapon = r.Weapon == AttackResolver.Thrown ? null : attacker.Weapon(r.Weapon)
            ?? throw RequestValidationException.For("weapon", L.T($"{attacker.Name} não tem essa arma pronta.", $"{attacker.Name} doesn't have that weapon ready."));
        if (weapon?.Jammed == true)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name} está travada: gaste uma Ação para destravar (CP:R p.344).", $"{weapon.Name} is jammed: spend an Action to unjam (CP:R p.344)."));

        // An empty magazine refuses before anyone is asked anything.
        if (weapon is { Magazine: not null, Melee: false } && (weapon.Rounds ?? 0) <= 0)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name} está sem munição; recarregue.", $"{weapon.Name} is out of ammo; reload."));

        // The attack roll first, with the extra d10 of a natural 10 or 1 (CP:R p.130), so it never changes afterwards.
        r = r with { AttackRoll = r.AttackRoll ?? dice.Roll(10) };
        if (r.AttackRoll is 10 or 1 && r.AttackExtra is null) r = r with { AttackExtra = dice.Roll(10) };
        var seq = s.AttackSeq + 1;

        foreach (var t in targets)
            if (!decided.Contains(t.Id) && NeedsDefense(weapon, r, t))
                return s with { Pending = new PendingAttack(seq, r, t.Id, PendingStep.Defense, decided) };

        // Every defense is known: preview the attack to fix the damage and the defense rolls left to the dice.
        var plan = resolver.Resolve(r, attacker, targets, dice);
        var defenses = new Dictionary<string, int>(r.DefenseRolls ?? []);
        foreach (var hit in plan.Hits.Where(h => h.DefenseRoll is not null)) defenses[hit.TargetId] = hit.DefenseRoll!.Value;
        r = r with
        {
            DefenseRolls = defenses,
            DamageDice = r.DamageDice ?? (plan.Hits.FirstOrDefault()?.Dice is { Count: > 0 } d && plan.Mode != AttackMode.Suppressive ? d.ToList() : null),
        };
        if (r.CritRoll is null && r.CritDice is null && plan.Hits.FirstOrDefault(h => h.Critical) is { } critical)
            return s with { Pending = new PendingAttack(seq, r, critical.TargetId, PendingStep.CriticalInjury, decided) };

        return await ApplyAttackAsync(s, e, notices, fighters, r, ct);
    }

    /// <summary>A ranged shot (not an area attack) at a target with REF 8+ that hasn't chosen yet (CP:R p.172).</summary>
    private bool NeedsDefense(WeaponView? weapon, AttackRequest r, FighterView target) =>
        !(weapon?.Melee ?? false)
        && r.Mode is not (AttackMode.Explosive or AttackMode.Suppressive or AttackMode.ShotgunShell)
        && target.CanDodge(combat.Document.DodgeRef.Value)
        && !r.Dodge
        && !(r.DodgeTargets?.Contains(target.Id) ?? false)
        && !(r.DefenseRolls?.ContainsKey(target.Id) ?? false);

    /// <summary>The target dodges (their Evasion roll becomes the DV) or takes the range DV; the attack goes on.</summary>
    public Task<EncounterDetailDto> AnswerDefenseAsync(int id, PendingDefenseRequest req, CancellationToken ct) =>
        MutateAsync(id, req.Version, async (s, e, notices) =>
        {
            var p = s.Pending is { Step: PendingStep.Defense } pending && pending.TargetId == req.TargetId ? pending
                : throw new ConflictException(L.T("Esse alvo não está decidindo uma esquiva agora.", "That target isn't choosing a dodge right now."));
            var fighters = await ViewsAsync(s, ct);
            var target = fighters[p.TargetId];
            var r = p.Request;
            if (req.Dodge)
                r = r with
                {
                    DodgeTargets = [.. r.DodgeTargets ?? [], p.TargetId],
                    DefenseRolls = new Dictionary<string, int>(r.DefenseRolls ?? []) { [p.TargetId] = dice.Roll(10) },
                };
            s = Log(s with { Pending = null }, target.Id, "dodge", req.Dodge
                ? L.T($"{target.Name} tenta esquivar (Evasion).", $"{target.Name} tries to dodge (Evasion).")
                : L.T($"{target.Name} não esquiva.", $"{target.Name} doesn't dodge."));
            return await ContinueAttackAsync(s, e, notices, fighters, r, [.. p.Decided, p.TargetId], ct);
        }, ct, allowPending: true);

    /// <summary>The attacker rolls the Critical Injury's 2d6 (CP:R p.187); the attack is then applied.</summary>
    public Task<EncounterDetailDto> RollPendingCriticalAsync(int id, PendingCriticalRequest req, CancellationToken ct) =>
        MutateAsync(id, req.Version, async (s, e, notices) =>
        {
            var p = s.Pending is { Step: PendingStep.CriticalInjury } pending && pending.TargetId == req.TargetId ? pending
                : throw new ConflictException(L.T("Não há Critical Injury esperando a rolagem.", "No Critical Injury is waiting for its roll."));
            var crit = new List<int> { dice.Roll(6), dice.Roll(6) };
            var r = p.Request with { CritDice = crit, CritRoll = crit.Sum() };
            var fighters = await ViewsAsync(s, ct);
            return await ContinueAttackAsync(s with { Pending = null }, e, notices, fighters, r, p.Decided, ct);
        }, ct, allowPending: true);

    /// <summary>The GM drops a pending attack: nothing was spent or applied.</summary>
    public Task<EncounterDetailDto> CancelPendingAsync(int id, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            if (s.Pending is null) throw new ConflictException(L.T("Não há ataque esperando.", "No attack is waiting."));
            return Task.FromResult(Log(s with { Pending = null }, null, "cancel", L.T("Ataque pendente cancelado pelo mestre.", "Pending attack cancelled by the GM.")));
        }, ct, allowPending: true);

    /// <summary>The pending attack as the screens show it (no attack total: the target decides without it).</summary>
    private PendingView? PendingViewOf(PendingAttack? p, Dictionary<string, FighterView> fighters)
    {
        if (p is null || !fighters.TryGetValue(p.Request.AttackerId, out var attacker) || !fighters.TryGetValue(p.TargetId, out var target)) return null;
        var weapon = p.Request.Weapon == AttackResolver.Thrown ? L.T("Objeto arremessado", "Thrown object") : attacker.Weapon(p.Request.Weapon)?.Name ?? p.Request.Weapon;
        return new PendingView(p.Seq, attacker.Id, attacker.Name, weapon, target.Id, target.Name, p.Step,
            p.Step == PendingStep.Defense ? resolver.RangeDvFor(p.Request, attacker) : null,
            p.Step == PendingStep.Defense ? target.Evasion : null,
            attacker.CharacterId, target.CharacterId);
    }
}
