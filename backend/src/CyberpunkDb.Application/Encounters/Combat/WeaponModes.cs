using System.Text.RegularExpressions;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters.Combat;

// What weapon-rules.json says about the mode being used. Null/false = the book's default.
public sealed record WeaponTraits
{
    public string? Source { get; init; }
    public string? Row { get; init; }
    public int? MaxRange { get; init; }
    public int? IgnoreArmorBelow { get; init; }
    public int? Ablation { get; init; }
    public bool Knockout { get; init; }
    public bool NoCrit { get; init; }
    public bool NoAblation { get; init; }
    public bool NoAimed { get; init; }
    public int? PerShot { get; init; }
    public int? MinRounds { get; init; }
    public bool AllRounds { get; init; }
    public string? ShortDamage { get; init; }
    public int? AutofireBullets { get; init; }
    public bool AutofireAllRounds { get; init; }
    public int? AutofireMinRounds { get; init; }
    public WeaponClass? AutofireDv { get; init; }
    public string? AutofireCheckSkill { get; init; }
    public int? AutofireCheckTotal { get; init; }
    public int? AutofireCheckDv { get; init; }
    public int? AutofireFailMultiplier { get; init; }
    public bool AutofireDestroys { get; init; }
    public string? Shells { get; init; }
    public string? ShellDamage { get; init; }
    public bool Explosive { get; init; }
    public string? AimedDamage { get; init; }
    public string? Drains { get; init; }
    public bool MeleeCharges { get; init; }
    public string? Effect { get; init; }
    public string? Fumble { get; init; }
    public string? FumbleNote { get; init; }
    public int? MinBody { get; init; }
    public bool MinBodyJam { get; init; }
    public string? MinBodyNote { get; init; }
    public string? Requires { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}

// Id null = the catalog's own magazine
public sealed record MagazineView(string? Id, string Label, int Rounds, int Capacity, bool Refill);

// What the fighter brings to every mode: skills, BODY and the weapon's running state.
public sealed record WeaponContext(
    Func<string, int> SkillTotal,
    Func<string, string> SkillLabel,
    int Body,
    // bonuses other than the skill and Excellent Quality (Smartgun, Precision Attack)
    IReadOnlyList<ValuePart> Bonuses,
    string? ActiveMode,
    // rounds in an extra magazine; null = never touched
    Func<string, int?> ExtraRounds,
    // NPCs start with every magazine full, PCs load them from the inventory
    bool ExtrasStartFull,
    bool Destroyed,
    int? MinBody);

public static partial class WeaponModes
{
    public const string Free = "free";
    public const string ThrowMode = "throw";
    public const string ThrownRow = "thrown";

    // "5d6 / 3d6" -> "5d6", "3d6 (tiro único)" -> "3d6"; anything else stays as it is
    public static string PrimaryDice(string damage)
    {
        var match = DiceRegex().Match(damage ?? "");
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : damage ?? "";
    }

    [GeneratedRegex(@"^\s*(\d+d\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DiceRegex();

    public static bool IsMeleeClass(WeaponClass? c) =>
        c is WeaponClass.LightMelee or WeaponClass.MediumMelee or WeaponClass.HeavyMelee or WeaponClass.VeryHeavyMelee;

    // The view the fight works with: the default mode on top, every mode in Modes (when there's more than one),
    // and the magazines when the weapon has extra ones.
    public static WeaponView Build(WeaponView v, string? slug, WeaponContext ctx, WeaponRuleBook book)
    {
        var rule = book.For(slug);
        var fallback = book.Fallback(v.Class);
        v = v with
        {
            Slug = slug,
            Damage = PrimaryDice(v.Damage),
            AutofireOnly = v.AutofireOnly == true || v.Skill == "autofire" ? true : v.AutofireOnly,
            Destroyed = ctx.Destroyed,
            Bolt = rule?.AfterShot == "bolt",
            ReturnsWith = rule?.Throw?.ReturnsWith ?? v.ReturnsWith,
        };
        var baseTraits = new WeaponTraits
        {
            Source = rule?.Source,
            Fumble = rule?.OnFumble,
            FumbleNote = rule?.FumbleNote?.ToString(),
            MinBody = ctx.MinBody,
            MinBodyJam = rule?.MinBodyEffect == "jam",
            MinBodyNote = rule?.MinBodyNote?.ToString(),
            Requires = rule?.Requires,
            Notes = [.. (rule?.Notes ?? []).Select(n => n.ToString()), .. fallback is null ? [] : new[] { fallback.Note.ToString() }],
        };
        v = Apply(v with { Traits = baseTraits }, rule?.Base, ctx);

        var magazines = Magazines(v, rule, ctx);
        var modes = new List<WeaponView>();
        if (rule?.Throw?.Only == true)
            modes.Add(Throw(v, rule.Throw, ctx));
        else
        {
            foreach (var m in rule?.Modes ?? [])
                modes.Add(Apply(v, m, ctx) with { ModeId = m.Id, ModeLabel = m.Label?.ToString() ?? m.Id, ModeSwitch = m.Switch ?? Free });
            if (modes.Count == 0) modes.Add(v);
            // any melee weapon can be thrown (CP:R p.177)
            if (IsMeleeClass(v.Class) && v.Melee)
            {
                if (modes.Count == 1 && modes[0].ModeId is null)
                    modes[0] = modes[0] with { ModeId = "melee", ModeLabel = L.T("Golpe", "Strike"), ModeSwitch = Free };
                modes.Add(Throw(v, rule?.Throw, ctx));
            }
        }

        // switched modes: the one stored on the weapon, else the first
        var switched = modes.Where(m => m.ModeSwitch is not null and not Free).ToList();
        var active = switched.FirstOrDefault(m => m.ModeId == ctx.ActiveMode) ?? switched.FirstOrDefault();
        modes = modes.Select(m => m with
        {
            ModeActive = m.ModeSwitch is null or Free || m.ModeId == active?.ModeId,
            Magazines = magazines,
            Rounds = m.MagazineId is { } id ? magazines?.FirstOrDefault(x => x.Id == id)?.Rounds : m.Rounds,
            Magazine = m.MagazineId is { } id2 ? magazines?.FirstOrDefault(x => x.Id == id2)?.Capacity : m.Magazine,
        }).ToList();
        var top = active ?? modes[0];
        return top with { Modes = modes.Count > 1 ? modes : null, Magazines = magazines };
    }

    // CP:R p.177: DEX + Athletics, Grenade Launcher row up to 25 m, deals the weapon's damage without halving SP
    // unless the weapon was made for throwing.
    private static WeaponView Throw(WeaponView v, ThrowRule? rule, WeaponContext ctx)
    {
        var total = ctx.SkillTotal("athletics") + ctx.Bonuses.Sum(b => b.Value) + (v.Quality == WeaponQuality.Excellent ? 1 : 0);
        return v with
        {
            ModeId = ThrowMode, ModeLabel = L.T("Arremessar", "Throw"), ModeSwitch = Free,
            Skill = "athletics", Melee = false, ThrownOnly = true, HalvesArmor = rule?.HalvesArmor ?? false, Rof = rule?.Rof ?? 1,
            AttackTotal = total, AttackParts = Parts(ctx, "athletics", v.Quality),
            AutofireMultiplier = null, AutofireTotal = null,
            Traits = (v.Traits ?? new WeaponTraits()) with { Row = ThrownRow },
        };
    }

    private static List<ValuePart> Parts(WeaponContext ctx, string skill, WeaponQuality quality) =>
    [
        new ValuePart(ctx.SkillLabel(skill), ctx.SkillTotal(skill)),
        .. ctx.Bonuses,
        .. quality == WeaponQuality.Excellent ? new[] { new ValuePart(L.T("Qualidade Excellent (CP:R p.344)", "Excellent Quality (CP:R p.344)"), 1) } : [],
    ];

    private static List<MagazineView>? Magazines(WeaponView v, WeaponRule? rule, WeaponContext ctx)
    {
        if (rule is null || rule.Magazines.Count == 0) return null;
        var list = new List<MagazineView>();
        if (v.Magazine is { } main) list.Add(new MagazineView(null, L.T("Pente", "Magazine"), v.Rounds ?? 0, main, false));
        foreach (var m in rule.Magazines)
        {
            var rounds = ctx.ExtraRounds(m.Id) ?? (ctx.ExtrasStartFull || m.Refill ? m.Capacity : 0);
            list.Add(new MagazineView(m.Id, m.Label.ToString(), Math.Clamp(rounds, 0, m.Capacity), m.Capacity, m.Refill));
        }
        return list;
    }

    private static WeaponView Apply(WeaponView v, WeaponModeRule? m, WeaponContext ctx)
    {
        if (m is null) return v;
        var skill = m.Skill ?? v.Skill;
        var quality = m.Quality ?? v.Quality;
        var melee = m.Thrown == true ? false : m.Melee ?? v.Melee;
        var attackTotal = v.AttackTotal;
        var parts = v.AttackParts;
        if (skill != v.Skill || quality != v.Quality)
        {
            parts = Parts(ctx, skill, quality);
            attackTotal = parts.Sum(p => p.Value);
        }
        var multiplier = m.NoAutofire == true ? null : m.AutofireMultiplier ?? v.AutofireMultiplier;
        int? autofireTotal = multiplier is null ? null
            : v.AutofireTotal ?? ctx.SkillTotal("autofire") + ctx.Bonuses.Sum(b => b.Value) + (quality == WeaponQuality.Excellent ? 1 : 0);
        var damage = m.Damage is { } d ? PrimaryDice(d) : v.Damage;
        if (m.BodyDamage is { } body && ctx.Body >= body.Body) damage = body.Damage;

        var t = v.Traits ?? new WeaponTraits();
        var nonLethal = m.NonLethal == true;
        var check = m.Autofire?.Check;
        t = t with
        {
            Row = m.Row ?? t.Row,
            MaxRange = m.MaxRange ?? t.MaxRange,
            IgnoreArmorBelow = m.IgnoreArmorBelow ?? t.IgnoreArmorBelow,
            Ablation = m.Ablation ?? t.Ablation,
            Knockout = t.Knockout || nonLethal || m.Knockout == true,
            NoCrit = t.NoCrit || nonLethal || m.NoCrit == true,
            NoAblation = t.NoAblation || nonLethal,
            NoAimed = t.NoAimed || m.NoAimed == true,
            PerShot = m.AllRounds == true ? null : m.Rounds ?? t.PerShot,
            MinRounds = m.MinRounds ?? t.MinRounds,
            AllRounds = m.AllRounds ?? t.AllRounds,
            ShortDamage = m.ShortDamage ?? t.ShortDamage,
            AutofireBullets = m.Autofire?.Bullets ?? t.AutofireBullets,
            AutofireAllRounds = m.Autofire?.AllRounds ?? t.AutofireAllRounds,
            AutofireMinRounds = m.Autofire?.MinRounds ?? t.AutofireMinRounds,
            AutofireDv = m.Autofire?.Dv ?? t.AutofireDv,
            AutofireCheckSkill = check?.Skill ?? t.AutofireCheckSkill,
            AutofireCheckTotal = check is not null ? ctx.SkillTotal(check.Skill) : t.AutofireCheckTotal,
            AutofireCheckDv = check?.Dv ?? t.AutofireCheckDv,
            AutofireFailMultiplier = check?.FailMultiplier ?? t.AutofireFailMultiplier,
            AutofireDestroys = m.Autofire?.Destroys ?? t.AutofireDestroys,
            Shells = m.Shells ?? t.Shells,
            ShellDamage = m.ShellDamage ?? t.ShellDamage,
            Explosive = m.Explosive ?? t.Explosive,
            AimedDamage = m.AimedDamage ?? t.AimedDamage,
            Drains = m.Drains ?? t.Drains,
            MeleeCharges = m.MeleeCharges ?? t.MeleeCharges,
            Effect = m.Effect?.ToString() ?? t.Effect,
            Notes = [.. t.Notes, .. m.Notes.Select(n => n.ToString())],
        };
        return v with
        {
            Skill = skill,
            Class = m.Class ?? v.Class,
            Quality = quality,
            AttackTotal = attackTotal,
            AttackParts = parts,
            Damage = damage,
            Rof = m.Rof ?? v.Rof,
            Melee = melee,
            // a mode that turns into a melee strike halves SP (CP:R p.176), one that stops being melee doesn't
            HalvesArmor = m.HalvesArmor ?? (m.Melee == true ? true : m.Melee == false || m.Thrown == true ? false : v.HalvesArmor),
            ThrownOnly = m.Thrown ?? v.ThrownOnly,
            AutofireMultiplier = multiplier,
            AutofireTotal = autofireTotal,
            AutofireOnly = m.AutofireOnly ?? v.AutofireOnly,
            MagazineId = m.Magazine ?? v.MagazineId,
            Traits = t,
        };
    }
}
