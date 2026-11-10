using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Encounters.Combat;

public sealed record PlannedHit(
    string TargetId, bool Hit, int? Dv, int? DefenseRoll, string Defense, IReadOnlyList<int> Dice, int Damage,
    InjuryLocation Location, bool HalveArmor, bool Critical, AttackMode Mode, bool Explosive, IReadOnlyList<string> Notes,
    // weapon-rules.json: armor under this SP is ignored, Unconscious at 1 HP instead of lower, no crits, ablation
    int? IgnoreArmorBelow = null, bool Knockout = false, bool NoCrit = false, bool NoAblation = false, int? Ablation = null);

public sealed record PlannedAttack(
    string WeaponName, string WeaponKey, AttackMode Mode, int? AttackRoll, int AttackTotal, int Rounds, IReadOnlyList<PlannedHit> Hits,
    IReadOnlyList<string> Notes, int? AttackExtra = null, bool Jammed = false,
    // the weapon mode used, the magazine paying for it and the one losing a charge on top
    string? ModeId = null, string? MagazineId = null, string? Drains = null,
    bool Destroyed = false,
    // thrown: out of the hand until picked up (or back with a Targeting Scope)
    bool LeavesHand = false,
    bool UsesAmmo = false);

// CP:R p.170-178. STAT + skill + 1d10 vs range DV or DEX + Evasion + 1d10 (defender wins ties). Autofire
// multiplies 2d6 by the margin; two or more 6s on damage = crit. Typed dice always win. Weapons with their own
// rules (weapon-rules.json) change the skill, damage, rounds per shot, armor and so on per mode.
public sealed class AttackResolver(CombatRuleBook combat, CharacterRuleBook characterRules)
{
    public const string Thrown = "thrown";

    // the mode the attack uses; refuses a mode the weapon doesn't have or hasn't switched to
    public static WeaponView SelectWeapon(FighterView attacker, string key, string? mode)
    {
        var weapon = attacker.Weapon(key) ?? throw RequestValidationException.For("weapon", L.T($"{attacker.Name} não tem essa arma pronta.", $"{attacker.Name} doesn't have that weapon ready."));
        var picked = weapon.Pick(mode) ?? throw RequestValidationException.For("weaponMode", L.T($"{weapon.Name} não tem o modo '{mode}'.", $"{weapon.Name} has no '{mode}' mode."));
        if (!picked.ModeActive)
            throw RequestValidationException.For("weaponMode", L.T($"{weapon.Name} está no modo {weapon.ModeLabel}: troque para {picked.ModeLabel} primeiro ({SwitchCost(picked.ModeSwitch)}).",
                $"{weapon.Name} is in {weapon.ModeLabel} mode: switch to {picked.ModeLabel} first ({SwitchCost(picked.ModeSwitch)})."));
        return picked;
    }

    public static string SwitchCost(string? kind) => kind switch
    {
        "move" => L.T("uma Move Action", "a Move Action"),
        "minute" => L.T("1 minuto", "1 minute"),
        _ => L.T("uma Ação", "an Action"),
    };

    public PlannedAttack Resolve(AttackRequest r, FighterView attacker, IReadOnlyList<FighterView> targets, IDiceRoller dice)
    {
        var doc = combat.Document;
        var autofireRule = characterRules.Document.Combat.Autofire;
        if (targets.Count == 0) throw RequestValidationException.For("targetIds", L.T("Escolha pelo menos um alvo.", "Pick at least one target."));
        var area = r.Mode is AttackMode.Explosive or AttackMode.ShotgunShell or AttackMode.Suppressive;
        if (!area && targets.Count > 1) throw RequestValidationException.For("targetIds", L.T("Só explosivos, Shotgun Shell e Suppressive Fire atingem mais de um alvo.", "Only explosives, Shotgun Shells and Suppressive Fire hit more than one target."));

        var thrown = r.Weapon == Thrown;
        var weapon = thrown ? ThrownObject(attacker, r) : SelectWeapon(attacker, r.Weapon, r.WeaponMode);
        var t = weapon.Traits ?? new WeaponTraits();
        string Cite(string text) => t.Source is { } source ? $"{text} ({source})" : text;

        if (weapon.Destroyed)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name} foi destruída: não dá mais para usar.", $"{weapon.Name} was destroyed: it can't be used anymore."));
        if (weapon.Thrown)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name} ainda não voltou: pegue-a do chão (ou espere o Targeting Scope trazê-la).", $"{weapon.Name} hasn't come back yet: pick it up (or wait for the Targeting Scope to bring it back)."));
        if (weapon.Jammed)
            throw RequestValidationException.For("weapon", weapon.Bolt
                ? L.T($"{weapon.Name}: gaste uma Ação para manobrar o ferrolho antes do próximo tiro.", $"{weapon.Name}: spend an Action to work the bolt before the next shot.")
                : L.T($"{weapon.Name} está travada: gaste uma Ação para destravar (CP:R p.344).", $"{weapon.Name} is jammed: spend an Action to unjam (CP:R p.344)."));
        if (t.Requires is { } needed && !attacker.HasCyberware(needed))
            throw RequestValidationException.For("weapon", Cite(L.T($"{weapon.Name} só funciona com {needed}.", $"{weapon.Name} only works with {needed}.")));

        var notes = new List<string>();
        var aimed = r.Mode is AttackMode.AimedHead or AttackMode.AimedHand or AttackMode.AimedLeg;
        var autofire = r.Mode is AttackMode.Autofire or AttackMode.Suppressive;
        if (aimed && t.NoAimed)
            throw RequestValidationException.For("mode", Cite(L.T($"{weapon.Name} não faz tiro mirado.", $"{weapon.Name} can't make Aimed Shots.")));
        if (!autofire && weapon.AutofireOnly == true)
            throw RequestValidationException.For("mode", L.T($"{weapon.Name} só dispara em Autofire.", $"{weapon.Name} only fires in Autofire."));
        if (!autofire && r.Mode != AttackMode.ShotgunShell && t.Shells == "only")
            throw RequestValidationException.For("mode", Cite(L.T($"{weapon.Name} só dispara Shotgun Shell.", $"{weapon.Name} only fires Shotgun Shells.")));
        if (aimed && weapon.Rof > doc.AimedShot.Value.MaxRof)
            notes.Add(L.T($"Tiro mirado usa a Action inteira com no máximo ROF {doc.AimedShot.Value.MaxRof} ({doc.AimedShot.Source}).", $"An aimed shot takes the whole Action with at most ROF {doc.AimedShot.Value.MaxRof} ({doc.AimedShot.Source})."));

        var rounds = 1;
        var shortShot = false;
        var usesAmmo = !thrown && weapon.UsesAmmo;
        var loaded = weapon.Rounds ?? 0;
        int baseTotal;
        if (autofire)
        {
            if (weapon.AutofireMultiplier is null || weapon.AutofireTotal is null)
                throw RequestValidationException.For("mode", L.T($"{weapon.Name} não tem Autofire ({autofireRule.Source}).", $"{weapon.Name} has no Autofire ({autofireRule.Source})."));
            baseTotal = weapon.AutofireTotal.Value;
            rounds = t.AutofireBullets ?? autofireRule.Value.Bullets;
            if (t.AutofireAllRounds && usesAmmo)
            {
                var min = t.AutofireMinRounds ?? 1;
                if (loaded < min)
                    throw RequestValidationException.For("weapon", Cite(L.T($"{weapon.Name} precisa de {min} bala(s) no pente para o Autofire; há {loaded}.", $"{weapon.Name} needs {min} round(s) in the magazine to Autofire; there are {loaded}.")));
                rounds = loaded;
            }
            else if (usesAmmo && loaded < rounds)
                throw RequestValidationException.For("weapon", L.T($"{weapon.Name} tem {loaded} bala(s) no pente; Autofire e Suppressive Fire precisam de {rounds}.", $"{weapon.Name} has {loaded} round(s) in the magazine; Autofire and Suppressive Fire need {rounds}."));
        }
        else
        {
            baseTotal = weapon.AttackTotal;
            if (usesAmmo)
            {
                var perShot = t.AllRounds ? Math.Max(loaded, 1) : t.PerShot ?? 1;
                var min = t.MinRounds ?? (t.AllRounds ? 1 : perShot);
                if (loaded < min)
                    throw RequestValidationException.For("weapon", min <= 1
                        ? L.T($"{weapon.Name} tem {loaded} bala(s) no pente; recarregue.", $"{weapon.Name} has {loaded} round(s) in the magazine; reload.")
                        : Cite(L.T($"{weapon.Name}: esse modo precisa de {min} bala(s) no pente; há {loaded}.", $"{weapon.Name}: that mode needs {min} round(s) in the magazine; there are {loaded}.")));
                rounds = Math.Min(perShot, loaded);
                shortShot = t.PerShot is { } full && rounds < full;
            }
        }
        if (t.Drains is { } drained && weapon.Magazines?.FirstOrDefault(m => m.Id == drained) is { } battery && battery.Rounds < 1)
            throw RequestValidationException.For("weapon", L.T($"{weapon.Name}: {battery.Label} sem carga; recarregue ou troque de modo.", $"{weapon.Name}: {battery.Label} is empty; reload it or switch modes."));

        if (aimed) baseTotal += doc.AimedShot.Value.Penalty;
        // +1 on Aimed Shots
        if (aimed && attacker.HasCyberware("targeting-scope"))
        {
            baseTotal += 1;
            notes.Add(L.T("Targeting Scope: +1 no tiro mirado.", "Targeting Scope: +1 to the aimed shot."));
        }
        baseTotal += r.Modifier;

        var attackRoll = CheckD10(r.AttackRoll, dice, "attackRoll");
        // natural 10 adds another d10, natural 1 subtracts one (CP:R p.130)
        int? extra = null;
        if (attackRoll == 10)
        {
            extra = CheckD10(r.AttackExtra, dice, "attackExtra");
            notes.Add(L.T($"Crítico: 10 natural, +{extra} (CP:R p.130).", $"Critical success: natural 10, +{extra} (CP:R p.130)."));
        }
        else if (attackRoll == 1)
        {
            // Fumble Recovery 4 ignores fumbles (CP:R p.146)
            if (attacker.Awareness("fumbleRecovery") >= 4) notes.Add(L.T("1 natural ignorado por Fumble Recovery (CP:R p.146).", "Natural 1 ignored by Fumble Recovery (CP:R p.146)."));
            else
            {
                extra = -CheckD10(r.AttackExtra, dice, "attackExtra");
                notes.Add(L.T($"Falha crítica: 1 natural, {extra} (CP:R p.130).", $"Critical failure: natural 1, {extra} (CP:R p.130)."));
            }
        }
        var attackTotal = baseTotal + attackRoll + (extra ?? 0);

        // Poor Quality jams on a natural 1 regardless of Fumble Recovery (CP:R p.344), unless the weapon has its
        // own fumble rule
        var jammed = attackRoll == 1 && weapon.Quality == WeaponQuality.Poor && !thrown && t.Fumble is null;
        if (jammed) notes.Add(L.T($"{weapon.Name} travou (Poor Quality, CP:R p.344): uma Ação para destravar.", $"{weapon.Name} jammed (Poor Quality, CP:R p.344): an Action to unjam."));
        var destroyed = false;
        if (attackRoll == 1 && !thrown && t.Fumble is "destroy" or "explode")
        {
            destroyed = true;
            notes.Add(Cite(L.T($"1 natural: {weapon.Name} se destruiu.", $"Natural 1: {weapon.Name} destroyed itself.") + (t.FumbleNote is { } why ? " " + why : "")));
        }
        else if (attackRoll == 1 && t.FumbleNote is { } fumbleNote) notes.Add(Cite(fumbleNote));
        if (autofire && t.AutofireDestroys)
        {
            destroyed = true;
            notes.Add(Cite(L.T($"{weapon.Name} derreteu depois do disparo: destruída.", $"{weapon.Name} melted after firing: destroyed.")));
        }
        if (!destroyed && weapon.Bolt)
        {
            jammed = true;
            notes.Add(Cite(L.T("Ferrolho: uma Ação para preparar o próximo tiro.", "Bolt action: an Action to chamber the next round.")));
        }
        if (!thrown && t.MinBody is { } minBody && attacker.Body < minBody)
        {
            if (t.MinBodyJam && !destroyed) jammed = true;
            notes.Add(Cite(t.MinBodyNote ?? L.T($"{weapon.Name} exige BODY {minBody} (a menos que esteja montada ou apoiada); {attacker.Name} tem {attacker.Body}.",
                $"{weapon.Name} needs BODY {minBody} (unless mounted or braced); {attacker.Name} has {attacker.Body}.")));
        }
        if (shortShot) notes.Add(Cite(L.T($"Só {rounds} bala(s) no pente: o tiro esvazia o pente.", $"Only {rounds} round(s) left: the shot empties the magazine.")));
        notes.AddRange(t.Notes.Select(Cite));

        // "Tommyknocker (Escopeta)" on the log and the result
        var shownName = weapon.Modes is null && weapon.ModeLabel is { } label && weapon.ModeId != "melee" ? $"{weapon.Name} ({label})" : weapon.Name;
        PlannedAttack Plan(IReadOnlyList<PlannedHit> hits) => new(shownName, weapon.Key, r.Mode, attackRoll, attackTotal, rounds, hits, notes, extra, jammed,
            weapon.ModeId, weapon.MagazineId, t.Drains, destroyed, !thrown && weapon.ThrownOnly, usesAmmo);

        // no damage, just the WILL check
        if (r.Mode == AttackMode.Suppressive)
        {
            var range = doc.Suppressive.Value.RangeMeters;
            var hits = targets.Select(target =>
            {
                var roll = CheckD10(TypedDefense(r, target.Id), dice, "defenseRolls");
                var check = target.Concentration + roll; // the Concentration total already holds WILL

                var held = check >= attackTotal;
                return new PlannedHit(target.Id, !held, attackTotal, check, $"WILL + Concentration + 1d10 = {check}", [], 0, InjuryLocation.Body, false, false,
                    r.Mode, false, [held ? L.T("Segurou a posição.", "Held their ground.") : L.T($"Precisa usar o próximo Move Action (e Run, se preciso) para ir para a cobertura ({doc.Suppressive.Source}).", $"Must use the next Move Action (and Run, if needed) to get to cover ({doc.Suppressive.Source}).")]);
            }).ToList();
            notes.Add(L.T($"Atinge quem está a pé, fora de cobertura e à vista, até {range} m.", $"Hits anyone on foot, out of cover and in sight, up to {range} m."));
            return Plan(hits);
        }

        // Rodeo: a Handgun check on top of the Autofire one, failing caps the multiplier
        var cap = weapon.AutofireMultiplier ?? 1;
        if (r.Mode == AttackMode.Autofire && t.AutofireCheckDv is { } checkDv)
        {
            var roll = dice.Roll(10);
            var check = (t.AutofireCheckTotal ?? 0) + roll;
            var passed = check > checkDv;
            if (!passed) cap = Math.Min(cap, t.AutofireFailMultiplier ?? cap);
            notes.Add(Cite(passed
                ? L.T($"Teste de {t.AutofireCheckSkill} DV{checkDv}: {check}, passou.", $"{t.AutofireCheckSkill} Check DV{checkDv}: {check}, passed.")
                : L.T($"Teste de {t.AutofireCheckSkill} DV{checkDv}: {check}, falhou: multiplicador máximo ×{cap}.", $"{t.AutofireCheckSkill} Check DV{checkDv}: {check}, failed: maximum multiplier ×{cap}.")));
        }

        // rolled once for area attacks; weapons with an effect instead of damage roll nothing
        var damageExpression = r.Mode switch
        {
            AttackMode.Autofire => autofireRule.Value.Damage,
            AttackMode.ShotgunShell => t.ShellDamage ?? doc.ShotgunShell.Value.Damage,
            _ when shortShot && t.ShortDamage is not null => t.ShortDamage,
            _ when aimed && t.AimedDamage is not null => t.AimedDamage,
            _ => weapon.Damage,
        };
        var effect = t.Effect;
        var damageDice = effect is null ? RollDice(damageExpression, r.DamageDice, dice) : [];
        var sixes = !t.NoCrit && damageDice.Count(d => d == 6) >= 2;

        var planned = new List<PlannedHit>();
        foreach (var target in targets)
        {
            var targetNotes = new List<string>();
            int? dv = null;
            int? defenseRoll = null;
            string defense;
            bool hit;
            if (weapon.Melee)
            {
                // melee is always opposed (CP:R p.176)
                defenseRoll = CheckD10(TypedDefense(r, target.Id), dice, "defenseRolls");
                dv = target.Evasion + defenseRoll;
                defense = $"Evasion {target.Evasion} + d10 {defenseRoll} = {dv}";
            }
            else if ((r.Dodge || (r.DodgeTargets?.Contains(target.Id) ?? false)) && target.CanDodge(doc.DodgeRef.Value) && r.Mode != AttackMode.Explosive)
            {
                defenseRoll = CheckD10(TypedDefense(r, target.Id), dice, "defenseRolls");
                dv = target.Evasion + defenseRoll;
                defense = $"esquiva (REF {target.Ref}): Evasion {target.Evasion} + d10 {defenseRoll} = {dv}";
            }
            else
            {
                if (r.Dodge && !target.CanDodge(doc.DodgeRef.Value)) targetNotes.Add(L.T($"{target.Name} tem REF {target.Ref}: só esquiva de tiros com REF {doc.DodgeRef.Value}+ ({doc.DodgeRef.Source}).", $"{target.Name} has REF {target.Ref}: only REF {doc.DodgeRef.Value}+ can dodge bullets ({doc.DodgeRef.Source})."));
                dv = RangeDv(weapon, r, thrown);
                defense = L.T($"DV{dv} pelo alcance", $"DV{dv} from range");
            }
            hit = attackTotal > dv; // defender wins ties (CP:R p.172/176)

            if (r.Mode == AttackMode.Explosive && hit && r.Dodge && target.CanDodge(doc.DodgeRef.Value))
            {
                // REF 8+ dodges a blast by beating the attack (CP:R p.174)
                defenseRoll = CheckD10(TypedDefense(r, target.Id), dice, "defenseRolls");
                var dodge = target.Evasion + defenseRoll.Value;
                if (dodge > attackTotal) { hit = false; targetNotes.Add(L.T($"Saiu da área da explosão (Evasion {dodge} contra {attackTotal}).", $"Got out of the blast area (Evasion {dodge} against {attackTotal}).")); }
            }

            var margin = r.Mode == AttackMode.Autofire && hit ? Math.Min(attackTotal - dv!.Value, cap) : 1;
            var amount = effect is not null ? 0 : damageDice.Sum() * margin;
            if (r.Mode == AttackMode.Autofire && hit)
                targetNotes.Add(L.T($"Autofire: {damageDice.Sum()} × {margin} (margem {attackTotal - dv!.Value}, máximo ×{cap}).", $"Autofire: {damageDice.Sum()} × {margin} (margin {attackTotal - dv!.Value}, maximum ×{cap})."));
            if (!hit && r.Mode == AttackMode.Explosive) targetNotes.Add(L.T("Errou o ponto: o Mestre decide onde a explosão caiu (CP:R p.174).", "Missed the spot: the GM decides where the explosion landed (CP:R p.174)."));
            if (hit && effect is not null) targetNotes.Add(Cite(effect));

            planned.Add(new PlannedHit(target.Id, hit, dv, defenseRoll, defense, damageDice, hit ? amount : 0,
                r.Mode == AttackMode.AimedHead ? InjuryLocation.Head : InjuryLocation.Body,
                weapon.HalvesArmor, hit && sixes, r.Mode, r.Mode == AttackMode.Explosive, targetNotes,
                t.IgnoreArmorBelow, t.Knockout, t.NoCrit, t.NoAblation, t.Ablation));
        }
        if (sixes) notes.Add(L.T("Dois ou mais 6 no dano: Critical Injury em quem foi atingido (CP:R p.187).", "Two or more 6s on the damage: Critical Injury to whoever was hit (CP:R p.187)."));
        if (r.Mode == AttackMode.ShotgunShell) notes.Add(L.T($"Shotgun Shell: todos à frente até {doc.ShotgunShell.Value.RangeMeters} m, dano rolado uma vez ({doc.ShotgunShell.Source}).", $"Shotgun Shell: everyone in front up to {doc.ShotgunShell.Value.RangeMeters} m, damage rolled once ({doc.ShotgunShell.Source})."));
        return Plan(planned);
    }

    // null when it can't be worked out
    public int? RangeDvFor(AttackRequest r, FighterView attacker)
    {
        var thrown = r.Weapon == Thrown;
        var weapon = thrown ? ThrownObject(attacker, r) : attacker.Weapon(r.Weapon)?.Pick(r.WeaponMode);
        if (weapon is null || weapon.Melee) return null;
        try
        {
            return RangeDv(weapon, r, thrown);
        }
        catch (RequestValidationException)
        {
            return null;
        }
    }

    // CP:R p.177: any object, DEX + Athletics
    private static WeaponView ThrownObject(FighterView attacker, AttackRequest r) =>
        new(Thrown, L.T("Objeto arremessado", "Thrown object"), WeaponClass.GrenadeLauncher, "athletics", attacker.Athletics, null,
            r.ThrownDamage ?? "1d6", 1, null, null, null, false, false);

    private int RangeDv(WeaponView weapon, AttackRequest r, bool thrown)
    {
        var doc = combat.Document;
        var t = weapon.Traits;
        var fallback = WeaponRuleBook.Embedded.Fallback(weapon.Class);
        if (r.Mode == AttackMode.ShotgunShell)
        {
            if (t?.Shells == "none" || (weapon.Class != WeaponClass.Shotgun && t?.Shells != "only"))
                throw RequestValidationException.For("mode", L.T($"{weapon.Name} não dispara Shotgun Shell.", $"{weapon.Name} doesn't fire Shotgun Shells."));
            return doc.ShotgunShell.Value.Dv;
        }
        var range = r.Range ?? throw RequestValidationException.For("range", L.T("Informe a distância até o alvo em metros.", "Enter the distance to the target in meters."));
        // thrown like any object (CP:R p.177)
        // a fired harpoon leaves the hand too, but on its own row
        thrown = thrown || t?.Row == WeaponModes.ThrownRow || (weapon.ThrownOnly && t?.Row is null);
        if (t?.MaxRange is { } max && range > max)
            throw RequestValidationException.For("range", L.T($"{weapon.Name} alcança só {max} m ({t.Source}).", $"{weapon.Name} only reaches {max} m ({t.Source})."));
        if (thrown && range > doc.Thrown.Value.MaxMeters)
            throw RequestValidationException.For("range", L.T($"Arremesso vai até {doc.Thrown.Value.MaxMeters} m ({doc.Thrown.Source}).", $"Throws reach up to {doc.Thrown.Value.MaxMeters} m ({doc.Thrown.Source})."));
        // printed range table wins, Autofire included
        if (weapon.RangeDv is { } printed) return combat.PrintedDv(weapon.Name, printed, range);
        if (r.Mode == AttackMode.Autofire)
        {
            var table = characterRules.Document.Combat.AutofireDv;
            var dvClass = t?.AutofireDv ?? weapon.Class;
            var row = table.Value.FirstOrDefault(x => x.WeaponClass == dvClass)
                      ?? (fallback is null ? null : table.Value.FirstOrDefault(x => x.WeaponClass == fallback.AutofireDv))
                      ?? throw RequestValidationException.For("mode", L.T($"{weapon.Name} não está na tabela de Autofire ({table.Source}).", $"{weapon.Name} isn't on the Autofire table ({table.Source})."));
            var band = combat.Band(range);
            if (band is null || band >= row.Dv.Length)
                throw RequestValidationException.For("range", L.T($"Autofire vai até {table.Bands[^1]} m ({table.Source}).", $"Autofire reaches up to {table.Bands[^1]} m ({table.Source})."));
            return row.Dv[band.Value];
        }
        var rangeRow = thrown ? combat.Row(doc.Thrown.Value.Row)
            : t?.Row is { } key ? combat.Row(key)
            : weapon.Class is { } c ? combat.RowFor(c) ?? (fallback is null ? null : combat.Row(fallback.Row)) : null;
        if (rangeRow is null) throw RequestValidationException.For("weapon", L.T($"{weapon.Name} não tem linha na tabela de alcance ({doc.SingleShot.Source}).", $"{weapon.Name} has no row on the range table ({doc.SingleShot.Source})."));
        return combat.SingleShotDv(rangeRow, range);
    }

    // typed dice must match count and faces
    public static List<int> RollDice(string expression, IReadOnlyList<int>? typed, IDiceRoller dice)
    {
        expression = WeaponModes.PrimaryDice(expression);
        var parts = expression.ToLowerInvariant().Split('d');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var count) || !int.TryParse(parts[1], out var sides))
        {
            // flat damage like fire (CP:R p.180)
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

    // a missing key isn't a 0
    private static int? TypedDefense(AttackRequest r, string targetId) =>
        r.DefenseRolls is { } rolls && rolls.TryGetValue(targetId, out var roll) ? roll : null;

    private static int CheckD10(int? typed, IDiceRoller dice, string field)
    {
        if (typed is null) return dice.Roll(10);
        return typed is >= 1 and <= 10 ? typed.Value : throw RequestValidationException.For(field, L.T("O d10 vai de 1 a 10.", "The d10 goes from 1 to 10."));
    }
}
