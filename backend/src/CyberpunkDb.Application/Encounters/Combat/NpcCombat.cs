using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Encounters.Combat;

public sealed record DamageOutcome(Combatant Combatant, int Sp, int Through, WoundState Before, WoundState After, IReadOnlyList<string> Notes);

/// <summary>
/// Damage, Critical Injuries, Death Saves and healing for NPCs and generic enemies, which live in the encounter. The
/// arithmetic (<see cref="DamageMath"/>) and the tables are the same the character sheet uses (CP:R p.186-188).
/// </summary>
public sealed class NpcCombat(CharacterRuleBook rules, CharacterCalculator calculator)
{
    public WoundState Wound(Combatant c) => calculator.WoundStateFor(c.Hp, c.Snapshot!.MaxHp);

    /// <summary>Subtracts SP, doubles head damage, lowers HP and ablates the location (CP:R p.186).</summary>
    public DamageOutcome Damage(Combatant c, int amount, InjuryLocation location, bool halveArmor, bool bypassArmor, bool fromAttack)
    {
        var notes = new List<string>();
        var before = Wound(c);
        var head = location == InjuryLocation.Head;
        var sp = DamageMath.EffectiveSp(head ? c.SpHead : c.SpBody, halveArmor, bypassArmor);
        var cracked = c.Injuries.Any(i => i.Name.Equals("Cracked Skull", StringComparison.OrdinalIgnoreCase));
        var through = DamageMath.Through(amount, sp, head, cracked);
        var next = c with { Hp = Math.Max(0, c.Hp - through), Stabilized = c.Stabilized && c.Hp - through >= 1 };
        if (through > 0 && !bypassArmor)
        {
            var ablation = rules.Document.Combat.AblationPerDamagingHit.Value;
            next = head ? next with { SpHead = Math.Max(0, next.SpHead - ablation) } : next with { SpBody = Math.Max(0, next.SpBody - ablation) };
        }
        if (fromAttack && before == WoundState.Mortally && through > 0)
        {
            var penalty = rules.Document.Derived.DeathSave.Value.PenaltyPerDamageWhileMortal;
            next = next with { DeathSavePenalty = next.DeathSavePenalty + penalty };
            notes.Add(L.T($"Atingido enquanto Mortally Wounded: penalidade de Death Save +{penalty} e Critical Injury (CP:R p.187).", $"Hit while Mortally Wounded: Death Save penalty +{penalty} and a Critical Injury (CP:R p.187)."));
        }
        var after = calculator.WoundStateFor(next.Hp, c.Snapshot!.MaxHp);
        return new DamageOutcome(next, sp, through, before, after, notes);
    }

    /// <summary>
    /// Rolls 2d6 on the body or head table until the result is an injury the target doesn't already have (CP:R p.187).
    /// </summary>
    public CriticalInjuryDefinition RollCritical(InjuryLocation location, IEnumerable<string> active, int? typed, IDiceRoller dice)
    {
        var table = location == InjuryLocation.Head ? rules.Document.Combat.CriticalInjuries.Head : rules.Document.Combat.CriticalInjuries.Body;
        var taken = active.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (table.All(i => taken.Contains(i.Name))) throw RequestValidationException.For("critical", L.T("O alvo já sofre todas as Critical Injuries dessa tabela.", "The target already has every Critical Injury on that table."));
        if (typed is { } t)
        {
            var pick = table.FirstOrDefault(i => i.Roll == t) ?? throw RequestValidationException.For("critRoll", L.T("O 2d6 vai de 2 a 12.", "The 2d6 goes from 2 to 12."));
            if (!taken.Contains(pick.Name)) return pick;
        }
        for (var i = 0; i < 200; i++)
        {
            var roll = dice.Roll(6) + dice.Roll(6);
            var injury = table.First(x => x.Roll == roll);
            if (!taken.Contains(injury.Name)) return injury;
        }
        return table.First(i => !taken.Contains(i.Name));
    }

    /// <summary>The injury and its 5 Bonus Damage straight to HP, regardless of SP (CP:R p.187).</summary>
    public Combatant AddCritical(Combatant c, CriticalInjuryDefinition injury, InjuryLocation location)
    {
        var bonus = rules.Document.Combat.CriticalInjury.Value.BonusDamage;
        var record = new CombatInjury(injury.Name, location, injury.Effect, injury.DeathSave, injury.ActionPenalty, injury.MovePenalty);
        return c with { Injuries = [.. c.Injuries, record], Hp = Math.Max(0, c.Hp - bonus) };
    }

    /// <summary>d10 + penalty under BODY; a natural 10 fails; each save adds +1 (CP:R p.188).</summary>
    public (Combatant Combatant, bool Survived, string Text) DeathSave(Combatant c, int roll)
    {
        if (Wound(c) != WoundState.Mortally) throw RequestValidationException.For("roll", L.T("Death Save só para quem está Mortally Wounded (CP:R p.188).", "Death Saves only for someone Mortally Wounded (CP:R p.188)."));
        if (c.Dead) throw RequestValidationException.For("roll", L.T($"{c.Name} já morreu.", $"{c.Name} is already dead."));
        var rule = rules.Document.Derived.DeathSave.Value;
        var penalty = c.Injuries.Count(i => i.DeathSave) + c.DeathSavePenalty;
        var total = roll + penalty;
        var survived = !(rule.NaturalTenFails && roll == 10) && total < c.Snapshot!.Body;
        var next = c with { DeathSavePenalty = c.DeathSavePenalty + rule.PenaltyPerSave, Dead = !survived };
        var text = $"Death Save: d10 {roll} + {penalty} = {total} contra BODY {c.Snapshot.Body}: {(survived ? "sobreviveu" : "morreu")}." + (roll == 10 ? L.T(" (10 natural sempre falha)", " (a natural 10 always fails)") : "");
        return (next, survived, text);
    }

    /// <summary>A Mortally Wounded target goes to 1 HP, unconscious, and the Death Save Penalty resets (CP:R p.188, p.222).</summary>
    public Combatant Stabilize(Combatant c)
    {
        if (c.Dead) throw RequestValidationException.For("target", L.T($"{c.Name} já morreu.", $"{c.Name} is already dead."));
        return Wound(c) == WoundState.Mortally
            ? c with { Hp = 1, DeathSavePenalty = 0, Stabilized = true, Conditions = c.Conditions.Append("Inconsciente").Distinct().ToList() }
            : c with { Stabilized = true };
    }

    public Combatant Heal(Combatant c, int amount)
    {
        if (amount <= 0) throw RequestValidationException.For("amount", L.T("Informe quanto HP recuperar.", "Enter how much HP to recover."));
        if (Wound(c) == WoundState.Mortally) throw RequestValidationException.For("amount", L.T("Mortally Wounded: estabilize primeiro (CP:R p.186).", "Mortally Wounded: stabilize first (CP:R p.186)."));
        return c with { Hp = Math.Min(c.Snapshot!.MaxHp, c.Hp + amount) };
    }
}
