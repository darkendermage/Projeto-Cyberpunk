using System.Text.Json;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters.Rules;

// weapon-rules.json: weapons that don't follow the book's default profile, by catalog slug. Shared by the sheet
// (extra magazines, modes) and the fight (how each mode attacks).
public sealed record WeaponRulesDocument
{
    public string About { get; init; } = "";
    public Ruled<Dictionary<WeaponClass, ClassFallback>> ClassFallbacks { get; init; } = new();
    public Dictionary<string, WeaponRule> Weapons { get; init; } = [];
    // implants that are weapons, by cyberware slug
    public Dictionary<string, CyberWeaponRule> Cyberweapons { get; init; } = [];
}

// A cyberware piece that attacks (Wolvers, Vampyres, Popup Shotgun...): its weapon profile, since the catalog keeps
// it as cyberware. Shots: the book's capacity, counted by hand (shown on the attack). Base/Notes as in WeaponRule.
public sealed record CyberWeaponRule
{
    public string Source { get; init; } = "";
    public WeaponClass Class { get; init; }
    public string Skill { get; init; } = "melee-weapon";
    public string Damage { get; init; } = "1d6";
    public int Rof { get; init; } = 1;
    public WeaponQuality Quality { get; init; } = WeaponQuality.Standard;
    public bool Exotic { get; init; }
    public int? Shots { get; init; }
    public WeaponModeRule? Base { get; init; }
    public List<Text> Notes { get; init; } = [];
}

// Classes the core tables don't list (Interface RED 5 machine guns)
public sealed record ClassFallback(string Row, WeaponClass AutofireDv, Text Note);

public sealed record Text(string Pt, string En)
{
    public override string ToString() => L.T(Pt, En);
}

public sealed record WeaponRule
{
    public string Source { get; init; } = "";
    // applied to every mode
    public WeaponModeRule? Base { get; init; }
    // empty = one mode, the catalog profile (plus Base)
    public List<WeaponModeRule> Modes { get; init; } = [];
    public List<MagazineRule> Magazines { get; init; } = [];
    // natural 1: "destroy", "explode" (destroyed, blows up on the spot) or "note"
    public string? OnFumble { get; init; }
    public Text? FumbleNote { get; init; }
    // "bolt": an Action to chamber the next round
    public string? AfterShot { get; init; }
    // below the catalog's minBody: "jam" after every shot, otherwise just the note
    public string? MinBodyEffect { get; init; }
    public Text? MinBodyNote { get; init; }
    // cyberware slug the weapon won't work without
    public string? Requires { get; init; }
    public ThrowRule? Throw { get; init; }
    public AttachmentRule? Attachments { get; init; }
    public List<Text> Notes { get; init; } = [];
}

// Attachment slots that differ from the default: 3 on non-exotic ranged weapons, none on exotics (CP:R p.95).
public sealed record AttachmentRule
{
    public string Source { get; init; } = "";
    // null keeps the default count
    public int? Slots { get; init; }
    // only these types fit, even on an exotic weapon (empty = the usual check)
    public List<WeaponAttachmentType> Types { get; init; } = [];
    public List<WeaponAttachmentType> ForbidTypes { get; init; } = [];
    public Text? Note { get; init; }
}

// Melee weapons can be thrown (CP:R p.177). Only = it can't be swung at all (Ranger boomerang).
public sealed record ThrowRule
{
    public bool Only { get; init; }
    public bool HalvesArmor { get; init; }
    public int? Rof { get; init; }
    public string? ReturnsWith { get; init; }
}

public sealed record MagazineRule
{
    public string Id { get; init; } = "";
    public Text Label { get; init; } = new("", "");
    public int Capacity { get; init; }
    // reloaded from the inventory like the main magazine
    public List<Caliber> Calibers { get; init; } = [];
    // battery or nets: an Action refills it without spending items
    public bool Refill { get; init; }
}

// Every field is optional: null keeps what the weapon already has.
public sealed record WeaponModeRule
{
    public string Id { get; init; } = "";
    public Text? Label { get; init; }
    // "free" (picked on each attack), "action", "move" or "minute" (stays on the weapon until switched back)
    public string? Switch { get; init; }
    public string? Skill { get; init; }
    public WeaponClass? Class { get; init; }
    // range row key (combat-rules.json singleShot) or "thrown"
    public string? Row { get; init; }
    public int? MaxRange { get; init; }
    public string? Damage { get; init; }
    public int? Rof { get; init; }
    public WeaponQuality? Quality { get; init; }
    public bool? Melee { get; init; }
    // thrown: leaves the hand
    public bool? Thrown { get; init; }
    public bool? HalvesArmor { get; init; }
    public int? IgnoreArmorBelow { get; init; }
    public int? Ablation { get; init; }
    // Unconscious at 1 HP, no Critical Injury, no ablation
    public bool? NonLethal { get; init; }
    // only the Unconscious at 1 HP part
    public bool? Knockout { get; init; }
    public bool? NoCrit { get; init; }
    public bool? NoAimed { get; init; }
    public bool? AutofireOnly { get; init; }
    public bool? NoAutofire { get; init; }
    public int? AutofireMultiplier { get; init; }
    public AutofireRule? Autofire { get; init; }
    // extra magazine it fires from (null = the main one)
    public string? Magazine { get; init; }
    // also takes a charge from this magazine
    public string? Drains { get; init; }
    // melee swings that still use a charge (stun baton side of the Bouncer)
    public bool? MeleeCharges { get; init; }
    // rounds per shot; fewer than MinRounds (default Rounds) refuses, otherwise fires what's left
    public int? Rounds { get; init; }
    public int? MinRounds { get; init; }
    public bool? AllRounds { get; init; }
    public string? ShortDamage { get; init; }
    // "only" or "none" (Shotgun Shell)
    public string? Shells { get; init; }
    public string? ShellDamage { get; init; }
    public bool? Explosive { get; init; }
    public string? AimedDamage { get; init; }
    public BodyDamageRule? BodyDamage { get; init; }
    // no damage: what the target has to do instead
    public Text? Effect { get; init; }
    public List<Text> Notes { get; init; } = [];
}

public sealed record AutofireRule
{
    public int? Bullets { get; init; }
    public bool? AllRounds { get; init; }
    public int? MinRounds { get; init; }
    // which Autofire DV row to use
    public WeaponClass? Dv { get; init; }
    public AutofireCheckRule? Check { get; init; }
    // destroyed after firing (Teen Dreem)
    public bool? Destroys { get; init; }
}

// an extra skill check with the shot; failing caps the multiplier
public sealed record AutofireCheckRule(string Skill, int Dv, int FailMultiplier);

public sealed record BodyDamageRule(int Body, string Damage);

public sealed class WeaponRuleBook
{
    private const string Resource = "CyberpunkDb.Application.Encounters.Rules.weapon-rules.json";

    private static readonly Lazy<WeaponRuleBook> EmbeddedBook = new(() =>
    {
        using var stream = typeof(WeaponRuleBook).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"Recurso {Resource} não encontrado.");
        return new WeaponRuleBook(JsonSerializer.Deserialize<WeaponRulesDocument>(stream, CombatRulesDocument.JsonOptions)
                                  ?? throw new InvalidOperationException("weapon-rules.json vazio."));
    });

    public WeaponRuleBook(WeaponRulesDocument document) => Document = document;

    // read-only data, loaded once
    public static WeaponRuleBook Embedded => EmbeddedBook.Value;

    public WeaponRulesDocument Document { get; }

    // a cyberweapon's traits and notes work like any weapon rule
    public WeaponRule? For(string? slug) =>
        slug is null ? null
        : Document.Weapons.GetValueOrDefault(slug)
          ?? (Cyber(slug) is { } c ? new WeaponRule { Source = c.Source, Base = c.Base, Notes = c.Notes } : null);

    public CyberWeaponRule? Cyber(string? slug) => slug is null ? null : Document.Cyberweapons.GetValueOrDefault(slug);

    public ClassFallback? Fallback(WeaponClass? weaponClass) =>
        weaponClass is { } c ? Document.ClassFallbacks.Value?.GetValueOrDefault(c) : null;

    public int AttachmentSlots(string? slug, bool ranged, bool exotic, int defaultSlots) =>
        For(slug)?.Attachments?.Slots ?? (ranged && !exotic ? defaultSlots : 0);

    // Whether the attachment can go on the weapon at all; free slots are checked when installing.
    public bool AttachmentFits(string? slug, bool exotic, WeaponAttachmentType type, bool exoticAllowed, int slotsRequired)
    {
        var rule = For(slug)?.Attachments;
        if (rule is null) return !exotic || exoticAllowed;
        if (rule.ForbidTypes.Contains(type)) return false;
        if (rule.Slots is { } slots && slotsRequired > slots) return false;
        return rule.Types.Count > 0 ? rule.Types.Contains(type) : !exotic || exoticAllowed;
    }

    // the catalog's magazine plus the extra ones the rule declares
    public MagazineRule? Magazine(string? slug, string? id) =>
        id is null ? null : For(slug)?.Magazines.FirstOrDefault(m => m.Id == id);
}
