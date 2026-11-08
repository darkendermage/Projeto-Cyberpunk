using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Encounters.Combat;

// everything but the d10 already added
public sealed record WeaponView(
    string Key, string Name, WeaponClass? Class, string Skill, int AttackTotal, int? AutofireTotal, string Damage, int Rof,
    int? Rounds, int? Magazine, int? AutofireMultiplier, bool Melee, bool HalvesArmor, int?[]? RangeDv = null, bool? AutofireOnly = null,
    // CP:R p.344
    WeaponQuality Quality = WeaponQuality.Standard, bool Jammed = false,
    // e.g. "Handgun 12", "Precision Attack +1", "Excellent +1"
    IReadOnlyList<ValuePart>? AttackParts = null,
    // Ranger boomerang
    bool ThrownOnly = false, bool Thrown = false, string? ReturnsWith = null);

public sealed record InjuryView(string Name, InjuryLocation Location, string? Effect, bool DeathSave);

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
    // NPCs only, for the read-only sheet
    IReadOnlyList<NamedValueDto>? Stats = null, IReadOnlyList<NamedValueDto>? Skills = null,
    // Solo only (CP:R p.146)
    IReadOnlyDictionary<string, int>? CombatAwareness = null,
    // PCs: Targeting Scope, boomerang return...
    IReadOnlyList<string>? Cyberware = null,
    // used this Round
    IReadOnlyList<string>? RoundFlags = null)
{
    public bool HasCyberware(string slug) => Cyberware?.Contains(slug, StringComparer.OrdinalIgnoreCase) ?? false;

    public int Awareness(string key) => CombatAwareness?.GetValueOrDefault(key) ?? 0;

    public bool CanDodge(int dodgeRef) => Ref >= dodgeRef;
    public bool CrackedSkull => Injuries.Any(i => i.Name.Equals("Cracked Skull", StringComparison.OrdinalIgnoreCase));
    public WeaponView? Weapon(string key) => Weapons.FirstOrDefault(w => w.Key == key);
}

public sealed class FighterViews(CharacterRuleBook characterRules, CharacterCalculator calculator, NpcRuleBook npcRules, CombatRuleBook combatRules)
{
    public const string Brawling = "brawling";
    public const string MartialArtsPrefix = "martial-arts";

    // player characters

    // combat-rules.json specialWeapons. The boomerang is thrown, never swung, still halves SP.
    private WeaponView Special(WeaponView v, string? slug, Combatant c)
    {
        if (slug is null || combatRules.Document.SpecialWeapons.Value?.GetValueOrDefault(slug) is not { } rule) return v;
        return v with
        {
            Melee = rule.ThrownOnly ? false : v.Melee,
            HalvesArmor = rule.HalvesArmor || v.HalvesArmor,
            Rof = rule.Rof ?? v.Rof,
            ThrownOnly = rule.ThrownOnly,
            Thrown = c.Thrown.Contains(v.Key),
            ReturnsWith = rule.ReturnsWith,
        };
    }

    public FighterView ForPc(Combatant c, CharacterDetailDto detail)
    {
        var r = detail.Report;
        int Stat(string key) => r.Stats.FirstOrDefault(s => s.Key == key)?.Effective ?? 0;
        int Skill(string id, string stat) => r.Skills.FirstOrDefault(s => s.SkillId == id && s.Specialization is null)?.Total ?? Stat(stat) + r.ActionPenalty;
        var body = r.Health.BodyForDerived;
        var cyberarm = r.Cyberware.Any(x => x.Slug.Equals("cyberarm", StringComparison.OrdinalIgnoreCase));

        var weapons = r.Weapons.Where(w => w.Equipped).Select(w =>
        {
            var bonus = w.AttackBonuses?.Sum(p => p.Value) ?? 0;
            return new WeaponView(
                w.InstanceId, w.Name, w.Class, w.SkillId ?? "", w.AttackTotal, w.AutofireTotal + bonus, w.Damage ?? "", w.Rof ?? 1,
                w.Magazine is null ? null : w.LoadedRounds, w.Magazine, w.AutofireMultiplier, w.SkillId == "melee-weapon", w.SkillId == "melee-weapon",
                Quality: w.Quality, Jammed: w.Jammed,
                AttackParts: [new ValuePart(w.SkillName ?? w.SkillId ?? w.Name, w.SkillTotal), .. w.AttackBonuses ?? []]);
        }).Select(v => Special(v, r.Weapons.First(x => x.InstanceId == v.Key).Slug, c)).ToList();
        var unarmed = Unarmed(body, cyberarm, Skill(Brawling, "DEX"),
            r.Skills.Where(s => s.SkillId == MartialArtsPrefix && s.Level > 0).Select(s => (s.Key, s.Specialization ?? "", s.Total)));
        // Precision Attack (CP:R p.146): sheet weapons already have it, unarmed attacks get it here.
        var awareness = detail.Sheet.Roles.FirstOrDefault(x => x.Role.Equals("Solo", StringComparison.OrdinalIgnoreCase))?.Split;
        var precision = Characters.Calculation.CharacterCalculator.PrecisionAttack(detail.Sheet);
        weapons.AddRange(precision == 0 ? unarmed : unarmed.Select(w => w with
        {
            AttackTotal = w.AttackTotal + precision,
            AttackParts = [.. w.AttackParts ?? [new ValuePart(w.Name, w.AttackTotal)], new ValuePart("Precision Attack", precision)],
        }));

        return new FighterView(c.Id, c.Kind, c.Name, c.Side, c.Initiative,
            r.Health.CurrentHp, r.Health.MaxHp, r.Health.WoundState, r.ActionPenalty, r.Health.StabilizationDv,
            r.Armor.Body.Sp, r.Armor.Head.Sp, r.Armor.Body.MaxSp ?? r.Armor.Body.Sp, r.Armor.Head.MaxSp ?? r.Armor.Head.Sp,
            Stat("REF"), Stat("DEX"), body, Stat("WILL"), Stat("MOVE"),
            Skill("evasion", "DEX"), Skill("athletics", "DEX"), Skill("concentration", "WILL"),
            weapons,
            r.ActiveInjuries.Where(i => i.Status == InjuryStatus.Active).Select(i => new InjuryView(i.Name, i.Location, i.Effect, i.RaisesDeathSave)).ToList(),
            [.. detail.Sheet.Combat.Conditions, .. c.Conditions],
            r.Health.DeathSavePenalty, r.Health.DeathSave, r.Health.MustRollDeathSave, detail.Sheet.Combat.Conditions.Contains("Morto"),
            detail.Sheet.Combat.Stabilized, c.CoverId, c.CharacterId, detail.Version, null,
            CombatAwareness: awareness, Cyberware: r.Cyberware.Select(x => x.Slug).ToList());
    }

    // NPCs and generic enemies

    public FighterView ForNpc(Combatant c)
    {
        var s = c.Snapshot ?? throw new InvalidOperationException(L.T($"{c.Name} não tem ficha de combate.", $"{c.Name} has no combat sheet."));
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
            w.Magazine is null ? null : c.Ammo.GetValueOrDefault(w.Key, w.Magazine.Value), w.Magazine, w.AutofireMultiplier, w.Melee, w.HalvesArmor ?? w.Melee, w.RangeDv, w.AutofireOnly,
            Jammed: c.Jammed.Contains(w.Key))).ToList();
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

    // damage by BODY (CP:R p.176, p.178)
    private IEnumerable<WeaponView> Unarmed(int body, bool cyberarm, int brawling, IEnumerable<(string Key, string Form, int Total)> martialArts)
    {
        var doc = combatRules.Document;
        yield return new WeaponView(Brawling, "Brawling (desarmado)", null, Brawling, brawling, null, combatRules.UnarmedDice(body, cyberarm), 2,
            null, null, null, true, doc.BrawlingHalvesArmor.Value);
        foreach (var (key, form, total) in martialArts)
            yield return new WeaponView(key, $"Martial Arts ({form})", null, key, total, null, combatRules.UnarmedDice(body, false), 2,
                null, null, null, true, doc.MartialArtsHalvesArmor.Value);
    }

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

    // Printed stat block. Blank stats are skipped, armored values win, weapons keep the printed attack total.
    public CombatSnapshot Compendium(CompendiumNpc npc)
    {
        var sheet = npc.Sheet ?? throw new InvalidOperationException($"{npc.Name} não tem ficha oficial.");
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in sheet.Stats)
            if (value is { } v) stats[key] = v;
        foreach (var (key, value) in sheet.ArmoredStats) stats[key] = value;

        var skills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in sheet.Skills)
            if (NpcCompendium.SkillKey(name, npcRules) is { } key)
                skills[key] = sheet.ArmoredSkills.TryGetValue(name, out var armored) ? armored : value;

        IEnumerable<CompendiumArmor> Worn(string location) => sheet.Armor.Where(a => a.Location.Equals(location, StringComparison.OrdinalIgnoreCase));
        int Sp(string location) => Worn(location).Select(a => a.Sp).DefaultIfEmpty(0).Max();
        // printed melee SP is the usual half unless the sheet says otherwise
        bool? FullSpVsMelee(string location) => Worn(location).Any(a => a.SpVsMelee is { } melee && melee != DamageMath.EffectiveSp(a.Sp, true, false)) ? true : null;
        var source = npc.Sources.FirstOrDefault();
        return new CombatSnapshot
        {
            CompendiumId = npc.Id,
            Source = source is null ? npc.Name : $"{source.Book} p.{source.Page}",
            MaxHp = sheet.Hp,
            Body = stats.GetValueOrDefault("BODY"),
            Stats = stats,
            Skills = skills,
            MaxSpBody = Sp("Body"),
            MaxSpHead = Sp("Head"),
            FullSpVsMeleeBody = FullSpVsMelee("Body"),
            FullSpVsMeleeHead = FullSpVsMelee("Head"),
            Cyberarm = sheet.Cyberware.Any(c => c.Contains("Cyberarm", StringComparison.OrdinalIgnoreCase)),
            Weapons = sheet.Weapons.Select((w, i) =>
            {
                var skill = NpcCompendium.SkillKey(w.Skill, npcRules) ?? w.Skill.ToLowerInvariant();
                return new CombatWeapon
                {
                    Key = $"w{i}", Name = w.Name, Class = w.Class, Skill = skill,
                    Damage = w.Damage, Rof = w.Rof, Magazine = w.Magazine, AutofireMultiplier = w.AutofireMultiplier, AutofireOnly = w.AutofireOnly, Melee = w.Melee, RangeDv = w.RangeDv,
                    AttackBase = w.AttackBase,
                    // Brawling doesn't halve armor unless the sheet says "Halves SP" (Adam Smasher) - CP:R p.176
                    HalvesArmor = skill == Brawling ? w.Notes?.Contains("Halves SP", StringComparison.OrdinalIgnoreCase) == true : null,
                };
            }).ToList(),
        };
    }

    // all typed by the GM, defaults in combat-rules.json
    public CombatSnapshot Generic(GenericRequest r)
    {
        var g = combatRules.Document.Generic.Value;
        var weaponClass = r.WeaponClass ?? g.WeaponClass;
        var melee = weaponClass is WeaponClass.LightMelee or WeaponClass.MediumMelee or WeaponClass.HeavyMelee or WeaponClass.VeryHeavyMelee;
        return new CombatSnapshot
        {
            Source = L.T("genérico", "generic"),
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
