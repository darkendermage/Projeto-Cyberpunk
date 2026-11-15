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
    // attacks by throwing and leaves the hand (Ranger boomerang, the Throw mode of melee weapons)
    bool ThrownOnly = false, bool Thrown = false, string? ReturnsWith = null,
    // weapon-rules.json: catalog slug, which mode this is, every mode, every magazine
    string? Slug = null, string? ModeId = null, string? ModeLabel = null, string? ModeSwitch = null, bool ModeActive = true,
    IReadOnlyList<WeaponView>? Modes = null, IReadOnlyList<MagazineView>? Magazines = null,
    // extra magazine this mode fires from (null = the main one)
    string? MagazineId = null,
    bool Destroyed = false,
    // bolt action: "jammed" after every shot until the bolt is worked
    bool Bolt = false,
    WeaponTraits? Traits = null)
{
    // null when the weapon has no such mode
    public WeaponView? Pick(string? modeId) =>
        modeId is null || (Modes is null && ModeId == modeId) ? this with { Modes = null } : Modes?.FirstOrDefault(m => m.ModeId == modeId);

    public bool UsesAmmo => Magazine is not null && (!Melee || Traits?.MeleeCharges == true);
}

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
    IReadOnlyList<string>? RoundFlags = null,
    // PCs: LUCK left this session (CP:R p.130)
    int? LuckAvailable = null,
    // house rule: -1 per dodge already tried this Round
    int DodgePenalty = 0,
    // a held Bulletproof Shield (CP:R p.184)
    int? ShieldHp = null, int? ShieldHpMax = null,
    // Kendo's Cut the Bullet, when the fighter meets it: DEX + Martial Arts (Kendo)
    int? CutTheBullet = null,
    // PCs: what their implants do at the table, the Initiative bonuses (Kerenzikov, Sandevistan on) and what's switched on
    IReadOnlyList<CyberTraitLine>? Traits = null, IReadOnlyList<ValuePart>? InitiativeParts = null,
    bool DodgeAnyRef = false, IReadOnlyDictionary<string, ActiveCyberState>? ActiveCyber = null)
{
    public bool HasCyberware(string slug) => Cyberware?.Contains(slug, StringComparer.OrdinalIgnoreCase) ?? false;

    public int Awareness(string key) => CombatAwareness?.GetValueOrDefault(key) ?? 0;

    // Reflex Co-Processor lets anyone dodge bullets (Black Chrome p.17)
    public bool CanDodge(int dodgeRef) => Ref >= dodgeRef || DodgeAnyRef;
    public bool CrackedSkull => Injuries.Any(i => i.Name.Equals("Cracked Skull", StringComparison.OrdinalIgnoreCase));
    public WeaponView? Weapon(string key) => Weapons.FirstOrDefault(w => w.Key == key);
}

// what a Special Move's requirements read: STATs, skill levels and the level in a form (null = unknown, allowed)
public sealed record MoveCheck(Func<string, int?> Stat, Func<string, int?> Skill, Func<string, int> FormLevel);

public sealed class FighterViews(CharacterRuleBook characterRules, CharacterCalculator calculator, NpcRuleBook npcRules, CombatRuleBook combatRules)
{
    public const string Brawling = "brawling";
    public const string MartialArtsPrefix = "martial-arts";

    // player characters

    public FighterView ForPc(Combatant c, CharacterDetailDto detail)
    {
        var r = detail.Report;
        var traits = r.CyberTraits ?? [];
        var on = c.ActiveCyber;
        // Berserk on (Edgerunners Mission Kit p.36): the Wound State penalty left after Pain Editor stops counting
        var berserk = traits.FirstOrDefault(t => t.Activate?.Effect == "ignoreWounds" && on.ContainsKey(t.Slug));
        var painEditor = r.Health.WoundState == WoundState.Seriously && traits.Any(t => t.Kind == "ignoreSeriously");
        var woundLeft = painEditor ? 0 : r.Health.WoundActionPenalty;
        var berserkFix = berserk is not null ? -woundLeft : 0;
        int Stat(string key) => r.Stats.FirstOrDefault(s => s.Key == key)?.Effective ?? 0;
        int Skill(string id, string stat) => (r.Skills.FirstOrDefault(s => s.SkillId == id && s.Specialization is null)?.Total ?? Stat(stat) + r.ActionPenalty) + berserkFix;
        var body = r.Health.BodyForDerived;
        // Gorilla Arm: weapons are wielded as BODY 11 (Edgerunners Mission Kit p.35)
        var wieldBody = traits.Where(t => t.Kind == "wieldBody").Select(t => t.Value ?? 0).DefaultIfEmpty(0).Max();
        var cyberarm = r.Cyberware.Any(x => x.Slug.Equals("cyberarm", StringComparison.OrdinalIgnoreCase));

        string SkillLabel(string id) => r.Skills.FirstOrDefault(s => s.SkillId == id && s.Specialization is null) is { } line ? L.SkillName(line.Name, line.NamePt) : id;
        var weapons = r.Weapons.Where(w => w.Equipped).Select(w =>
        {
            var bonus = w.AttackBonuses?.Sum(p => p.Value) ?? 0;
            var view = new WeaponView(
                w.InstanceId, w.Name, w.Class, w.SkillId ?? "", w.AttackTotal + berserkFix, w.AutofireTotal + bonus + berserkFix, w.Damage ?? "", w.Rof ?? 1,
                w.Magazine is null ? null : w.LoadedRounds, w.Magazine, w.AutofireMultiplier, w.SkillId == "melee-weapon", w.SkillId == "melee-weapon",
                Quality: w.Quality, Jammed: w.Jammed, Thrown: c.Thrown.Contains(w.InstanceId),
                AttackParts: [new ValuePart(w.SkillName ?? w.SkillId ?? w.Name, w.SkillTotal), .. w.AttackBonuses ?? [], .. berserkFix != 0 ? [new ValuePart(berserk!.Label, berserkFix)] : Array.Empty<ValuePart>()]);
            var bonuses = (w.AttackBonuses ?? []).Where(p => !p.Label.StartsWith(L.T("Qualidade Excellent", "Excellent Quality"), StringComparison.Ordinal)).ToList();
            var context = new WeaponContext(id => Skill(id, SkillStat(id)), SkillLabel, Math.Max(body, wieldBody), bonuses, w.Mode,
                id => w.Magazines?.TryGetValue(id, out var n) == true ? n : null, false, w.Destroyed, w.MinBody);
            return WeaponModes.Build(view, w.Slug, context, WeaponRuleBook.Embedded);
        }).ToList();
        int? Level(string id) => r.Skills.FirstOrDefault(s => s.SkillId == id && s.Specialization is null)?.Level ?? 0;
        var forms = r.Skills.Where(s => s.SkillId == MartialArtsPrefix && s.Level > 0).ToList();
        var check = new MoveCheck(k => Stat(k), Level, form => forms.FirstOrDefault(s => s.Specialization?.Equals(form, StringComparison.OrdinalIgnoreCase) == true)?.Level ?? 0);
        var unarmed = Unarmed(body, cyberarm, Skill(Brawling, "DEX"), forms.Select(s => (s.Key, s.Specialization ?? "", s.Total + berserkFix)), check);
        // Precision Attack (CP:R p.146): sheet weapons already have it, unarmed attacks (and their strikes) get it here.
        var awareness = detail.Sheet.Roles.FirstOrDefault(x => x.Role.Equals("Solo", StringComparison.OrdinalIgnoreCase))?.Split;
        var precision = Characters.Calculation.CharacterCalculator.PrecisionAttack(detail.Sheet);
        weapons.AddRange(precision == 0 ? unarmed : unarmed.Select(w => WithPrecision(w, precision)));
        // the shield in hand: Bulletproof Shield or Popup Shield (CP:R p.184, p.365)
        var shield = r.Health;
        // Cut the Bullet needs a melee weapon in hand (Interface RED 4 p.79)
        var kendo = forms.FirstOrDefault(s => s.Specialization?.Equals("Kendo", StringComparison.OrdinalIgnoreCase) == true);
        var cut = MartialArtsRuleBook.Embedded.Move("Kendo", "cut-the-bullet")?.Move;
        int? cutTheBullet = kendo is not null && cut is not null && MartialArtsRuleBook.Meets(cut, k => Stat(k), Level, kendo.Level)
                            && weapons.Any(w => w.Melee && w.Key != Brawling && !w.Key.StartsWith(MartialArtsPrefix, StringComparison.Ordinal))
            ? kendo.Total : null;

        return new FighterView(c.Id, c.Kind, c.Name, c.Side, c.Initiative,
            r.Health.CurrentHp, r.Health.MaxHp, r.Health.WoundState, r.ActionPenalty + berserkFix, r.Health.StabilizationDv,
            r.Armor.Body.Sp, r.Armor.Head.Sp, r.Armor.Body.MaxSp ?? r.Armor.Body.Sp, r.Armor.Head.MaxSp ?? r.Armor.Head.Sp,
            Stat("REF"), Stat("DEX"), body, Stat("WILL"), Stat("MOVE"),
            Skill("evasion", "DEX"), Skill("athletics", "DEX"), Skill("concentration", "WILL"),
            weapons,
            r.ActiveInjuries.Where(i => i.Status == InjuryStatus.Active).Select(i => new InjuryView(i.Name, i.Location, i.Effect, i.RaisesDeathSave)).ToList(),
            [.. detail.Sheet.Combat.Conditions, .. c.Conditions],
            r.Health.DeathSavePenalty, r.Health.DeathSave, r.Health.MustRollDeathSave, detail.Sheet.Combat.Conditions.Contains("Morto"),
            detail.Sheet.Combat.Stabilized, c.CoverId, c.CharacterId, detail.Version, null,
            CombatAwareness: awareness, Cyberware: r.Cyberware.Select(x => x.Slug).ToList(), LuckAvailable: r.LuckAvailable,
            ShieldHp: shield?.ShieldHp, ShieldHpMax: shield?.ShieldHpMax, CutTheBullet: cutTheBullet,
            Traits: traits, InitiativeParts: InitiativeParts(traits, on), DodgeAnyRef: traits.Any(t => t.Kind == "dodgeAnyRef"), ActiveCyber: on);
    }

    // Kerenzikov +2 always; a Speedware switched on (Sandevistan +3) replaces what it excludes (CP:R p.359, Speedware doesn't stack)
    public static IReadOnlyList<ValuePart> InitiativeParts(IReadOnlyList<CyberTraitLine> traits, IReadOnlyDictionary<string, ActiveCyberState> on)
    {
        var active = traits.Where(t => t.Activate?.Effect == "initiative" && on.ContainsKey(t.Slug)).ToList();
        var excluded = active.SelectMany(t => t.Activate!.Excludes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return traits.Where(t => t.Kind == "initiative" && !excluded.Contains(t.Slug)).Select(t => new ValuePart(t.Label, t.Add))
            .Concat(active.Select(t => new ValuePart(t.Label, t.Activate!.Add))).ToList();
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

        var weapons = s.Weapons.Select(w =>
        {
            var view = new WeaponView(
                w.Key, w.Name, w.Class, w.Skill, (w.AttackBase ?? Skill(w.Skill)) + (w.AttackBase is null ? 0 : penalty),
                w.AutofireMultiplier is null ? null : Skill("autofire"), w.Damage, w.Rof,
                w.Magazine is null ? null : c.Ammo.GetValueOrDefault(w.Key, w.Magazine.Value), w.Magazine, w.AutofireMultiplier, w.Melee, w.HalvesArmor ?? w.Melee, w.RangeDv, w.AutofireOnly,
                Jammed: c.Jammed.Contains(w.Key), Thrown: c.Thrown.Contains(w.Key));
            var context = new WeaponContext(Skill, SkillLabel, s.Body, [], c.Modes.GetValueOrDefault(w.Key),
                id => c.Ammo.TryGetValue(ExtraAmmoKey(w.Key, id), out var n) ? n : null, true, c.Destroyed.Contains(w.Key), w.MinBody);
            return WeaponModes.Build(view, w.Slug, context, WeaponRuleBook.Embedded);
        }).ToList();
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

    // NPC ammo for an extra magazine (Tommyknocker shotgun, Crowd Buster battery...)
    public static string ExtraAmmoKey(string weaponKey, string magazine) => $"{weaponKey}#{magazine}";

    // the skill's own STAT for PCs without the skill
    private string SkillStat(string id) => npcRules.FindSkill(id)?.Stat ?? "DEX";

    private string SkillLabel(string key)
    {
        var parts = key.Split(':', 2);
        var name = npcRules.FindSkill(parts[0])?.Name ?? parts[0];
        return parts.Length == 2 ? $"{name} ({parts[1]})" : name;
    }

    // damage by BODY (CP:R p.176, p.178)
    private IEnumerable<WeaponView> Unarmed(int body, bool cyberarm, int brawling, IEnumerable<(string Key, string Form, int Total)> martialArts, MoveCheck? check = null)
    {
        var doc = combatRules.Document;
        yield return new WeaponView(Brawling, "Brawling (desarmado)", null, Brawling, brawling, null, combatRules.UnarmedDice(body, cyberarm), 2,
            null, null, null, true, doc.BrawlingHalvesArmor.Value);
        foreach (var (key, form, total) in martialArts)
            yield return WithStrikes(new WeaponView(key, $"Martial Arts ({form})", null, key, total, null, combatRules.UnarmedDice(body, false), 2,
                null, null, null, true, doc.MartialArtsHalvesArmor.Value), form, check);
    }

    // The form's attack Special Moves as free modes of its Martial Arts attack: one strike (ROF 1) in place of the two
    // attacks, with the move's modifier, to the head when it says so, and its Critical Injury on a hit.
    private static WeaponView WithStrikes(WeaponView basic, string form, MoveCheck? check)
    {
        if (MartialArtsRuleBook.Embedded.Form(form) is not { } rule) return basic;
        var level = check?.FormLevel(form) ?? int.MaxValue;
        var moves = rule.Moves.Where(m => m.Kind == "attack" && (check is null || MartialArtsRuleBook.Meets(m, check.Stat, check.Skill, level))).ToList();
        if (moves.Count == 0) return basic;
        var top = basic with { ModeId = "strike", ModeLabel = L.T("2 ataques", "2 attacks"), ModeSwitch = WeaponModes.Free };
        var modes = new List<WeaponView> { top };
        foreach (var move in moves)
        foreach (var strike in move.Strikes)
            modes.Add(basic with
            {
                ModeId = $"move:{move.Id}:{strike.Id}",
                ModeLabel = strike.Label is { } label ? $"{move.Name} {label}" : move.Name,
                ModeSwitch = WeaponModes.Free,
                Rof = 1,
                AttackTotal = basic.AttackTotal + strike.Modifier,
                AttackParts = strike.Modifier == 0 ? basic.AttackParts
                    : [.. basic.AttackParts ?? [new ValuePart(basic.Name, basic.AttackTotal)], new ValuePart(move.Name, strike.Modifier)],
                Traits = new WeaponTraits
                {
                    Source = MartialArtsRuleBook.Page(rule), NoAimed = true, HitsHead = strike.Head,
                    InjuryOnHit = strike.Injury, ConditionOnHit = strike.Condition, OpposedBy = strike.Versus,
                    Effect = strike.NoDamage ? move.Effect.ToString() : null, NoCrit = strike.NoDamage,
                    Notes = strike.NoDamage ? [] : [move.Effect.ToString()],
                },
            });
        return top with { Modes = modes };
    }

    private static WeaponView WithPrecision(WeaponView w, int precision) => w with
    {
        AttackTotal = w.AttackTotal + precision,
        AttackParts = [.. w.AttackParts ?? [new ValuePart(w.Name, w.AttackTotal)], new ValuePart("Precision Attack", precision)],
        Modes = w.Modes?.Select(m => WithPrecision(m, precision)).ToList(),
    };

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
                Slug = catalog.Find(line.ItemId)?.Slug, MinBody = w.MinBody,
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
