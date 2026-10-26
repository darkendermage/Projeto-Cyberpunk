using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Encounters.Combat;

/// <summary>What the attack does to one target before it is applied to a sheet or to the encounter.</summary>
public sealed record PlannedHit(
    string TargetId, bool Hit, int? Dv, int? DefenseRoll, string Defense, IReadOnlyList<int> Dice, int Damage,
    InjuryLocation Location, bool HalveArmor, bool Critical, AttackMode Mode, bool Explosive, IReadOnlyList<string> Notes);

public sealed record PlannedAttack(
    string WeaponName, string WeaponKey, AttackMode Mode, int? AttackRoll, int AttackTotal, int Rounds, IReadOnlyList<PlannedHit> Hits,
    IReadOnlyList<string> Notes);

/// <summary>
/// Resolves an attack the way the Core does (CP:R p.170-178): the attacker's STAT + skill + 1d10 against the range DV
/// or the defender's DEX + Evasion + 1d10 (defender wins ties); then the damage dice, with Autofire multiplying 2d6 by
/// the margin, and a Critical Injury when two or more damage dice come up 6. Typed dice always win over rolled ones.
/// </summary>
public sealed class AttackResolver(CombatRuleBook combat, CharacterRuleBook characterRules)
{
    public const string Thrown = "thrown";

    public PlannedAttack Resolve(AttackRequest r, FighterView attacker, IReadOnlyList<FighterView> targets, IDiceRoller dice)
    {
        var doc = combat.Document;
        if (targets.Count == 0) throw RequestValidationException.For("targetIds", L.T("Escolha pelo menos um alvo.", "Pick at least one target."));
        var area = r.Mode is AttackMode.Explosive or AttackMode.ShotgunShell or AttackMode.Suppressive;
        if (!area && targets.Count > 1) throw RequestValidationException.For("targetIds", L.T("Só explosivos, Shotgun Shell e Suppressive Fire atingem mais de um alvo.", "Only explosives, Shotgun Shells and Suppressive Fire hit more than one target."));

        var thrown = r.Weapon == Thrown;
        var weapon = thrown
            ? new WeaponView(Thrown, L.T("Objeto arremessado", "Thrown object"), WeaponClass.GrenadeLauncher, "athletics", attacker.Athletics, null,
                r.ThrownDamage ?? "1d6", 1, null, null, null, false, false)
            : attacker.Weapon(r.Weapon) ?? throw RequestValidationException.For("weapon", L.T($"{attacker.Name} não tem essa arma pronta.", $"{attacker.Name} doesn't have that weapon ready."));

        var notes = new List<string>();
        var aimed = r.Mode is AttackMode.AimedHead or AttackMode.AimedHand or AttackMode.AimedLeg;
        if (aimed && weapon.Rof > doc.AimedShot.Value.MaxRof)
            notes.Add(L.T($"Tiro mirado usa a Action inteira com no máximo ROF {doc.AimedShot.Value.MaxRof} ({doc.AimedShot.Source}).", $"An aimed shot takes the whole Action with at most ROF {doc.AimedShot.Value.MaxRof} ({doc.AimedShot.Source})."));

        // ---------------------------------------------------------------- the attack check
        var autofire = r.Mode is AttackMode.Autofire or AttackMode.Suppressive;
        var rounds = 1;
        int baseTotal;
        if (autofire)
        {
            if (weapon.AutofireMultiplier is null || weapon.AutofireTotal is null)
                throw RequestValidationException.For("mode", L.T($"{weapon.Name} não tem Autofire ({characterRules.Document.Combat.Autofire.Source}).", $"{weapon.Name} has no Autofire ({characterRules.Document.Combat.Autofire.Source})."));
            baseTotal = weapon.AutofireTotal.Value;
            rounds = characterRules.Document.Combat.Autofire.Value.Bullets;
        }
        else baseTotal = weapon.AttackTotal;
        if (aimed) baseTotal += doc.AimedShot.Value.Penalty;
        baseTotal += r.Modifier;
        if (weapon.Rounds is { } loaded && weapon.Magazine is not null && loaded < rounds)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name} tem {loaded} bala(s) no pente; {(autofire ? "Autofire e Suppressive Fire precisam de " + rounds : "recarregue")}.", $"{weapon.Name} has {loaded} round(s) in the magazine; {(autofire ? "Autofire and Suppressive Fire need " + rounds : "reload")}."));

        var attackRoll = CheckD10(r.AttackRoll, dice, "attackRoll");
        var attackTotal = baseTotal + attackRoll;

        // ---------------------------------------------------------------- Suppressive Fire: no damage, only the WILL check
        if (r.Mode == AttackMode.Suppressive)
        {
            var range = doc.Suppressive.Value.RangeMeters;
            var hits = targets.Select(t =>
            {
                var roll = CheckD10(r.DefenseRolls?.GetValueOrDefault(t.Id), dice, "defenseRolls");
                var check = t.Concentration + roll; // the Concentration total already holds WILL

                var held = check >= attackTotal;
                return new PlannedHit(t.Id, !held, attackTotal, check, $"WILL + Concentration + 1d10 = {check}", [], 0, InjuryLocation.Body, false, false,
                    r.Mode, false, [held ? L.T("Segurou a posição.", "Held their ground.") : L.T($"Precisa usar o próximo Move Action (e Run, se preciso) para ir para a cobertura ({doc.Suppressive.Source}).", $"Must use the next Move Action (and Run, if needed) to get to cover ({doc.Suppressive.Source}).")]);
            }).ToList();
            notes.Add(L.T($"Atinge quem está a pé, fora de cobertura e à vista, até {range} m.", $"Hits anyone on foot, out of cover and in sight, up to {range} m."));
            return new PlannedAttack(weapon.Name, weapon.Key, r.Mode, attackRoll, attackTotal, rounds, hits, notes);
        }

        // ---------------------------------------------------------------- damage (rolled once for area attacks)
        var damageExpression = r.Mode switch
        {
            AttackMode.Autofire => characterRules.Document.Combat.Autofire.Value.Damage,
            AttackMode.ShotgunShell => doc.ShotgunShell.Value.Damage,
            _ => weapon.Damage,
        };
        var damageDice = RollDice(damageExpression, r.DamageDice, dice);
        var sixes = damageDice.Count(d => d == 6) >= 2;

        var planned = new List<PlannedHit>();
        foreach (var t in targets)
        {
            var targetNotes = new List<string>();
            int? dv = null;
            int? defenseRoll = null;
            string defense;
            bool hit;
            if (weapon.Melee)
            {
                // CP:R p.176: melee is always opposed by DEX + Evasion + 1d10.
                defenseRoll = CheckD10(r.DefenseRolls?.GetValueOrDefault(t.Id), dice, "defenseRolls");
                dv = t.Evasion + defenseRoll;
                defense = $"Evasion {t.Evasion} + d10 {defenseRoll} = {dv}";
            }
            else if (r.Dodge && t.CanDodge(doc.DodgeRef.Value) && r.Mode != AttackMode.Explosive)
            {
                defenseRoll = CheckD10(r.DefenseRolls?.GetValueOrDefault(t.Id), dice, "defenseRolls");
                dv = t.Evasion + defenseRoll;
                defense = $"esquiva (REF {t.Ref}): Evasion {t.Evasion} + d10 {defenseRoll} = {dv}";
            }
            else
            {
                if (r.Dodge && !t.CanDodge(doc.DodgeRef.Value)) targetNotes.Add(L.T($"{t.Name} tem REF {t.Ref}: só esquiva de tiros com REF {doc.DodgeRef.Value}+ ({doc.DodgeRef.Source}).", $"{t.Name} has REF {t.Ref}: only REF {doc.DodgeRef.Value}+ can dodge bullets ({doc.DodgeRef.Source})."));
                dv = RangeDv(weapon, r, thrown);
                defense = L.T($"DV{dv} pelo alcance", $"DV{dv} from range");
            }
            hit = attackTotal > dv; // CP:R p.172/p.176: the defender wins ties

            if (r.Mode == AttackMode.Explosive && hit && r.Dodge && t.CanDodge(doc.DodgeRef.Value))
            {
                // CP:R p.174: REF 8+ dodges a blast by rolling higher than the attack.
                defenseRoll = CheckD10(r.DefenseRolls?.GetValueOrDefault(t.Id), dice, "defenseRolls");
                var dodge = t.Evasion + defenseRoll.Value;
                if (dodge > attackTotal) { hit = false; targetNotes.Add(L.T($"Saiu da área da explosão (Evasion {dodge} contra {attackTotal}).", $"Got out of the blast area (Evasion {dodge} against {attackTotal}).")); }
            }

            var amount = r.Mode == AttackMode.Autofire && hit
                ? damageDice.Sum() * Math.Min(attackTotal - dv!.Value, weapon.AutofireMultiplier!.Value)
                : damageDice.Sum();
            if (r.Mode == AttackMode.Autofire && hit)
                targetNotes.Add(L.T($"Autofire: {damageDice.Sum()} × {Math.Min(attackTotal - dv!.Value, weapon.AutofireMultiplier!.Value)} (margem {attackTotal - dv.Value}, máximo ×{weapon.AutofireMultiplier}).", $"Autofire: {damageDice.Sum()} × {Math.Min(attackTotal - dv!.Value, weapon.AutofireMultiplier!.Value)} (margin {attackTotal - dv.Value}, maximum ×{weapon.AutofireMultiplier})."));
            if (!hit && r.Mode == AttackMode.Explosive) targetNotes.Add(L.T("Errou o ponto: o Mestre decide onde a explosão caiu (CP:R p.174).", "Missed the spot: the GM decides where the explosion landed (CP:R p.174)."));

            planned.Add(new PlannedHit(t.Id, hit, dv, defenseRoll, defense, damageDice, hit ? amount : 0,
                r.Mode == AttackMode.AimedHead ? InjuryLocation.Head : InjuryLocation.Body,
                weapon.HalvesArmor && !thrown, hit && sixes, r.Mode, r.Mode == AttackMode.Explosive, targetNotes));
        }
        if (sixes) notes.Add(L.T("Dois ou mais 6 no dano: Critical Injury em quem foi atingido (CP:R p.187).", "Two or more 6s on the damage: Critical Injury to whoever was hit (CP:R p.187)."));
        if (r.Mode == AttackMode.ShotgunShell) notes.Add(L.T($"Shotgun Shell: todos à frente até {doc.ShotgunShell.Value.RangeMeters} m, dano rolado uma vez ({doc.ShotgunShell.Source}).", $"Shotgun Shell: everyone in front up to {doc.ShotgunShell.Value.RangeMeters} m, damage rolled once ({doc.ShotgunShell.Source})."));
        return new PlannedAttack(weapon.Name, weapon.Key, r.Mode, attackRoll, attackTotal, rounds, planned, notes);
    }

    private int RangeDv(WeaponView weapon, AttackRequest r, bool thrown)
    {
        var doc = combat.Document;
        if (r.Mode == AttackMode.ShotgunShell)
        {
            if (weapon.Class != WeaponClass.Shotgun) throw RequestValidationException.For("mode", L.T("Shotgun Shell só sai de escopeta.", "Shotgun Shells only come out of shotguns."));
            return doc.ShotgunShell.Value.Dv;
        }
        var range = r.Range ?? throw RequestValidationException.For("range", L.T("Informe a distância até o alvo em metros.", "Enter the distance to the target in meters."));
        if (thrown && range > doc.Thrown.Value.MaxMeters)
            throw RequestValidationException.For("range", L.T($"Arremesso vai até {doc.Thrown.Value.MaxMeters} m ({doc.Thrown.Source}).", $"Throws reach up to {doc.Thrown.Value.MaxMeters} m ({doc.Thrown.Source})."));
        if (r.Mode == AttackMode.Autofire)
        {
            var table = characterRules.Document.Combat.AutofireDv;
            var row = table.Value.FirstOrDefault(x => x.WeaponClass == weapon.Class)
                      ?? throw RequestValidationException.For("mode", L.T($"{weapon.Name} não está na tabela de Autofire ({table.Source}).", $"{weapon.Name} isn't on the Autofire table ({table.Source})."));
            var band = combat.Band(range);
            if (band is null || band >= row.Dv.Length)
                throw RequestValidationException.For("range", L.T($"Autofire vai até {table.Bands[^1]} m ({table.Source}).", $"Autofire reaches up to {table.Bands[^1]} m ({table.Source})."));
            return row.Dv[band.Value];
        }
        var rangeRow = thrown ? combat.Row(doc.Thrown.Value.Row)
            : weapon.Class is { } c ? combat.RowFor(c) : null;
        if (rangeRow is null) throw RequestValidationException.For("weapon", L.T($"{weapon.Name} não tem linha na tabela de alcance ({doc.SingleShot.Source}).", $"{weapon.Name} has no row on the range table ({doc.SingleShot.Source})."));
        return combat.SingleShotDv(rangeRow, range);
    }

    /// <summary>Rolls (or takes) the dice of an expression like "3d6"; the typed dice must match the count and faces.</summary>
    public static List<int> RollDice(string expression, IReadOnlyList<int>? typed, IDiceRoller dice)
    {
        var parts = expression.ToLowerInvariant().Split('d');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var count) || !int.TryParse(parts[1], out var sides))
        {
            // A flat amount ("2", "6") deals that damage, as fire does (CP:R p.180).
            return int.TryParse(expression, out var flat) ? [flat] : throw RequestValidationException.For("damage", L.T($"Dano '{expression}' não é uma expressão de dados.", $"Damage '{expression}' isn't a dice expression."));
        }
        if (typed is { Count: > 0 })
        {
            if (typed.Count != count || typed.Any(d => d < 1 || d > sides))
                throw RequestValidationException.For("damageDice", L.T($"Informe {count} dado(s) de 1 a {sides} ({expression}).", $"Enter {count} die/dice from 1 to {sides} ({expression})."));
            return [.. typed];
        }
        return Enumerable.Range(0, count).Select(_ => dice.Roll(sides)).ToList();
    }

    private static int CheckD10(int? typed, IDiceRoller dice, string field)
    {
        if (typed is null) return dice.Roll(10);
        return typed is >= 1 and <= 10 ? typed.Value : throw RequestValidationException.For(field, L.T("O d10 vai de 1 a 10.", "The d10 goes from 1 to 10."));
    }
}
