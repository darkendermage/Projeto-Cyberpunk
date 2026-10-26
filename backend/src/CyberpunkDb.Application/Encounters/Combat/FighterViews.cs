using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters.Combat;

/// <summary>An attack option with everything but the d10 already added (STAT + skill + penalties).</summary>
public sealed record WeaponView(
    string Key, string Name, WeaponClass? Class, string Skill, int AttackTotal, int? AutofireTotal, string Damage, int Rof,
    int? Rounds, int? Magazine, int? AutofireMultiplier, bool Melee, bool HalvesArmor);

public sealed record InjuryView(string Name, InjuryLocation Location, string? Effect, bool DeathSave);

/// <summary>A combatant as the combat table sees it, whatever its source (character sheet, NPC sheet or generic).</summary>
public sealed record FighterView(
    string Id, CombatantKind Kind, string Name, CombatSide Side, int? Initiative,
    int Hp, int MaxHp, WoundState WoundState, int ActionPenalty, int? StabilizationDv,
    int SpBody, int SpHead, int MaxSpBody, int MaxSpHead,
    int Ref, int Dex, int Body, int Will, int Move,
    int Evasion, int Athletics, int Concentration,
    IReadOnlyList<WeaponView> Weapons,
    IReadOnlyList<InjuryView> Injuries, IReadOnlyList<string> Conditions,
    int DeathSavePenalty, int DeathSaveTarget, bool MustDeathSave, bool Dead, bool Stabilized,
    string? CoverId, int? CharacterId, int? CharacterVersion, string? Source,
    // NPCs and generic enemies: the STATs and Skills of the snapshot, for the read-only sheet. PCs read their own sheet.
    IReadOnlyList<NamedValueDto>? Stats = null, IReadOnlyList<NamedValueDto>? Skills = null)
{
    public bool CanDodge(int dodgeRef) => Ref >= dodgeRef;
    public bool CrackedSkull => Injuries.Any(i => i.Name.Equals("Cracked Skull", StringComparison.OrdinalIgnoreCase));
    public WeaponView? Weapon(string key) => Weapons.FirstOrDefault(w => w.Key == key);
}

/// <summary>Builds <see cref="FighterView"/>s and NPC snapshots. Wound States, penalties and Death Saves use the character rules.</summary>
public sealed class FighterViews(CharacterRuleBook characterRules, CharacterCalculator calculator, NpcRuleBook npcRules, CombatRuleBook combatRules)
{
    public const string Brawling = "brawling";
    public const string MartialArtsPrefix = "martial-arts";

    // ------------------------------------------------------------------ player characters

    public FighterView ForPc(Combatant c, CharacterDetailDto detail)
    {
        var r = detail.Report;
        int Stat(string key) => r.Stats.FirstOrDefault(s => s.Key == key)?.Effective ?? 0;
        int Skill(string id, string stat) => r.Skills.FirstOrDefault(s => s.SkillId == id && s.Specialization is null)?.Total ?? Stat(stat) + r.ActionPenalty;
        var body = r.Health.BodyForDerived;
        var cyberarm = r.Cyberware.Any(x => x.Slug.Equals("cyberarm", StringComparison.OrdinalIgnoreCase));

        var weapons = r.Weapons.Where(w => w.Equipped).Select(w => new WeaponView(
            w.InstanceId, w.Name, w.Class, w.SkillId ?? "", w.SkillTotal, w.AutofireTotal, w.Damage ?? "", w.Rof ?? 1,
            w.Magazine is null ? null : w.LoadedRounds, w.Magazine, w.AutofireMultiplier, w.SkillId == "melee-weapon", w.SkillId == "melee-weapon")).ToList();
        weapons.AddRange(Unarmed(body, cyberarm, Skill(Brawling, "DEX"),
            r.Skills.Where(s => s.SkillId == MartialArtsPrefix && s.Level > 0).Select(s => (s.Key, s.Specialization ?? "", s.Total))));

        return new FighterView(c.Id, c.Kind, c.Name, c.Side, c.Initiative,
            r.Health.CurrentHp, r.Health.MaxHp, r.Health.WoundState, r.ActionPenalty, r.Health.StabilizationDv,
            r.Armor.Body.Sp, r.Armor.Head.Sp, r.Armor.Body.MaxSp ?? r.Armor.Body.Sp, r.Armor.Head.MaxSp ?? r.Armor.Head.Sp,
            Stat("REF"), Stat("DEX"), body, Stat("WILL"), Stat("MOVE"),
            Skill("evasion", "DEX"), Skill("athletics", "DEX"), Skill("concentration", "WILL"),
            weapons,
            r.ActiveInjuries.Where(i => i.Status == InjuryStatus.Active).Select(i => new InjuryView(i.Name, i.Location, i.Effect, i.RaisesDeathSave)).ToList(),
            [.. detail.Sheet.Combat.Conditions, .. c.Conditions],
            r.Health.DeathSavePenalty, r.Health.DeathSave, r.Health.MustRollDeathSave, detail.Sheet.Combat.Conditions.Contains("Morto"),
            detail.Sheet.Combat.Stabilized, c.CoverId, c.CharacterId, detail.Version, null);
    }

    // ------------------------------------------------------------------ NPCs and generic enemies

    public FighterView ForNpc(Combatant c)
    {
        var s = c.Snapshot ?? throw new InvalidOperationException($"{c.Name} não tem ficha de combate.");
        var wound = calculator.WoundStateFor(c.Hp, s.MaxHp);
        var d = characterRules.Document.Derived;
        var (woundAction, woundMove) = wound switch
        {
            WoundState.Seriously => (d.SeriouslyWounded.Value.ActionPenalty, 0),
            WoundState.Mortally => (d.MortallyWounded.Value.ActionPenalty, d.MortallyWounded.Value.MovePenalty),
            _ => (0, 0),
        };
        var penalty = woundAction + c.Injuries.Sum(i => i.ActionPenalty ?? 0);
        int Stat(string key) => s.Stats.GetValueOrDefault(key);
        int Skill(string key)
        {
            if (s.Skills.TryGetValue(key, out var b)) return b + penalty;
            var stat = npcRules.FindSkill(key.Split(':')[0])?.Stat ?? "DEX";
            return Stat(stat) + penalty;
        }
        int? Dv(WoundState w) => w switch
        {
            WoundState.Lightly => d.StabilizationDv.Value.Lightly,
            WoundState.Seriously => d.StabilizationDv.Value.Seriously,
            WoundState.Mortally => d.StabilizationDv.Value.Mortally,
            _ => null,
        };

        var weapons = s.Weapons.Select(w => new WeaponView(
            w.Key, w.Name, w.Class, w.Skill, (w.AttackBase ?? Skill(w.Skill)) + (w.AttackBase is null ? 0 : penalty),
            w.AutofireMultiplier is null ? null : Skill("autofire"), w.Damage, w.Rof,
            w.Magazine is null ? null : c.Ammo.GetValueOrDefault(w.Key, w.Magazine.Value), w.Magazine, w.AutofireMultiplier, w.Melee, w.Melee)).ToList();
        weapons.AddRange(Unarmed(s.Body, s.Cyberarm, Skill(Brawling),
            s.Skills.Keys.Where(k => k.StartsWith(MartialArtsPrefix + ":", StringComparison.OrdinalIgnoreCase)).Select(k => (k, k.Split(':')[1], Skill(k)))));

        var basePenalty = c.Injuries.Count(i => i.DeathSave);
        var move = Math.Max(Stat("MOVE") + woundMove + c.Injuries.Sum(i => i.MovePenalty ?? 0), wound == WoundState.Mortally ? d.MortallyWounded.Value.MoveMinimum : 0);
        return new FighterView(c.Id, c.Kind, c.Name, c.Side, c.Initiative,
            c.Hp, s.MaxHp, wound, penalty, Dv(wound),
            c.SpBody, c.SpHead, s.MaxSpBody, s.MaxSpHead,
            Stat("REF"), Stat("DEX"), s.Body, Stat("WILL"), move,
            Skill("evasion"), Skill("athletics"), Skill("concentration"),
            weapons,
            c.Injuries.Select(i => new InjuryView(i.Name, i.Location, i.Effect, i.DeathSave)).ToList(), c.Conditions,
            basePenalty + c.DeathSavePenalty, s.Body, wound == WoundState.Mortally && !c.Dead, c.Dead, c.Stabilized,
            c.CoverId, null, null, s.Source,
            s.Stats.Select(x => new NamedValueDto(x.Key, x.Key.ToUpperInvariant(), x.Value)).ToList(),
            s.Skills.Select(x => new NamedValueDto(x.Key, SkillLabel(x.Key), Skill(x.Key))).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private string SkillLabel(string key)
    {
        var parts = key.Split(':', 2);
        var name = npcRules.FindSkill(parts[0])?.Name ?? parts[0];
        return parts.Length == 2 ? $"{name} ({parts[1]})" : name;
    }

    /// <summary>Brawling and each Martial Arts form the combatant knows, with damage by BODY (CP:R p.176, p.178).</summary>
    private IEnumerable<WeaponView> Unarmed(int body, bool cyberarm, int brawling, IEnumerable<(string Key, string Form, int Total)> martialArts)
    {
        var doc = combatRules.Document;
        yield return new WeaponView(Brawling, "Brawling (desarmado)", null, Brawling, brawling, null, combatRules.UnarmedDice(body, cyberarm), 2,
            null, null, null, true, doc.BrawlingHalvesArmor.Value);
        foreach (var (key, form, total) in martialArts)
            yield return new WeaponView(key, $"Martial Arts ({form})", null, key, total, null, combatRules.UnarmedDice(body, false), 2,
                null, null, null, true, doc.MartialArtsHalvesArmor.Value);
    }

    /// <summary>The combat side of an NPC sheet: effective STATs, Skill Bases, worn SP, weapons and Death Save BODY.</summary>
    public CombatSnapshot Snapshot(NpcSheet sheet, NpcValidationReport report, NpcCatalog catalog, int? npcId, string source)
    {
        var weapons = new List<CombatWeapon>();
        foreach (var line in report.GearLines.Where(g => g.Category == ItemCategory.Weapon))
        {
            if (catalog.Find(line.ItemId)?.Weapon is not { } w) continue;
            var skill = npcRules.SkillIdFor(w.Skill) ?? "handgun";
            weapons.Add(new CombatWeapon
            {
                Key = $"w{line.Index}", Name = line.Name, Class = w.Class, Skill = skill, Damage = w.Damage ?? "1d6", Rof = w.Rof ?? 1,
                Magazine = w.Magazine, AutofireMultiplier = w.AutofireMultiplier, Melee = w.Skill == WeaponSkill.MeleeWeapon,
            });
        }
        var d = report.Derived;
        return new CombatSnapshot
        {
            NpcId = npcId,
            Source = source,
            MaxHp = d.Hp,
            Body = d.BodyForDerived,
            Stats = new Dictionary<string, int>(d.EffectiveStats, StringComparer.OrdinalIgnoreCase),
            Skills = report.SkillLines.Where(l => l.Level > 0).ToDictionary(l => l.Key, l => l.Base, StringComparer.OrdinalIgnoreCase),
            MaxSpBody = d.SpBody,
            MaxSpHead = d.SpHead,
            Cyberarm = report.CyberLines.Any(l => l.Slug.Equals("cyberarm", StringComparison.OrdinalIgnoreCase)),
            Weapons = weapons,
        };
    }

    /// <summary>A quick enemy without a sheet: every value typed by the GM (defaults in combat-rules.json, complementary).</summary>
    public CombatSnapshot Generic(GenericRequest r)
    {
        var g = combatRules.Document.Generic.Value;
        var weaponClass = r.WeaponClass ?? g.WeaponClass;
        var melee = weaponClass is WeaponClass.LightMelee or WeaponClass.MediumMelee or WeaponClass.HeavyMelee or WeaponClass.VeryHeavyMelee;
        return new CombatSnapshot
        {
            Source = "genérico",
            MaxHp = r.Hp ?? g.Hp,
            Body = r.Body ?? g.Body,
            Stats = new(StringComparer.OrdinalIgnoreCase)
            {
                ["REF"] = r.Ref ?? g.Ref, ["DEX"] = r.Dex ?? g.Dex, ["BODY"] = r.Body ?? g.Body, ["WILL"] = r.Will ?? g.Will, ["MOVE"] = r.Move ?? g.Move,
            },
            Skills = new(StringComparer.OrdinalIgnoreCase) { ["evasion"] = r.EvasionBase ?? g.EvasionBase },
            MaxSpBody = r.SpBody ?? g.SpBody,
            MaxSpHead = r.SpHead ?? g.SpHead,
            Weapons =
            [
                new CombatWeapon
                {
                    Key = "w0", Name = string.IsNullOrWhiteSpace(r.WeaponName) ? EnumName(weaponClass) : r.WeaponName.Trim(), Class = weaponClass,
                    Skill = melee ? "melee-weapon" : "handgun", Damage = string.IsNullOrWhiteSpace(r.Damage) ? g.Damage : r.Damage.Trim(),
                    Rof = r.Rof ?? (melee || weaponClass is WeaponClass.MediumPistol or WeaponClass.HeavyPistol ? 2 : 1),
                    AttackBase = r.AttackBase ?? g.AttackBase, Melee = melee,
                },
            ],
        };
    }

    private static string EnumName(WeaponClass c) => Common.EnumLabels.Label(c);
}
