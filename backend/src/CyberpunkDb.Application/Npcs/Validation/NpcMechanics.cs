using System.Text.RegularExpressions;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Validation;

/// <summary>Mechanical helpers shared by the validator and the generator, so both read the rules the same way.</summary>
public sealed partial class NpcMechanics(NpcRuleBook rules)
{
    public NpcRuleBook Rules { get; } = rules;

    // ------------------------------------------------------------ prices

    /// <summary>
    /// Unit price of a gear entry. Weapons bought in another quality use the CP:R p.342 table; returns
    /// <c>null</c> price with an error message when no official price exists for that quality.
    /// </summary>
    public (int? Price, WeaponQuality? Quality, string? Error) GearUnitPrice(CatalogItem item, WeaponQuality? requested)
    {
        if (item.Weapon is not { } w || requested is null || requested == w.Quality) return (item.Price, item.Weapon?.Quality, null);
        if (w.Exotic)
            return (null, requested, $"{item.Name} é uma arma exótica e só existe na qualidade cadastrada ({Rules.Document.WeaponQualityPricing.ExoticRule.Source}).");
        if (w.Quality != WeaponQuality.Standard)
            return (null, requested, $"{item.Name} já é uma variante de qualidade {Label(w.Quality)}; escolha a versão padrão para trocar a qualidade.");
        var row = Rules.Document.WeaponQualityPricing.Rows.FirstOrDefault(r => r.Standard == item.Price);
        if (row is null)
            return (null, requested, $"Não há preço oficial de {Label(requested.Value)} para uma arma de {item.Price}eb ({Rules.Document.WeaponQualityPricing.Source} cobre só 50, 100 e 500eb).");
        return (requested == WeaponQuality.Poor ? row.Poor : row.Excellent, requested, null);
    }

    public static string Label(WeaponQuality q) => q switch
    {
        WeaponQuality.Poor => "Poor Quality",
        WeaponQuality.Excellent => "Excellent Quality",
        _ => "Standard",
    };

    // ------------------------------------------------------------ cyberware structure

    /// <summary>Slots taken in the parent or pool. Missing data defaults to 1 (CP:R p.111).</summary>
    public (int Slots, bool Assumed) SlotsRequired(CatalogItem item)
    {
        if (Rules.FindOverride(item.Slug)?.SlotsRequired is { } o) return (o, false);
        if (item.Cyber?.SlotsRequired is { } s) return (s, false);
        return (Rules.Document.Cyberware.OptionSlotDefault.Value, true);
    }

    public int? SlotsProvided(CatalogItem item) =>
        Rules.FindOverride(item.Slug)?.SlotsProvided ?? item.Cyber?.SlotsProvided;

    /// <summary>
    /// True when <paramref name="parent"/> can host an option that requires <paramref name="required"/>:
    /// the same item or an equivalent foundational piece (same type, body location and own prerequisite,
    /// e.g. a Sponsored Cybereye for a Cybereye, but never a Chipware Socket for a Neural Link).
    /// </summary>
    public static bool ParentCompatible(CatalogItem parent, CatalogItem required) =>
        parent.Id == required.Id
        || (parent.Cyber is { Foundational: true } p && required.Cyber is { Foundational: true } r
            && p.Type == r.Type && p.Location == r.Location && p.RequiresItemId == r.RequiresItemId);

    /// <summary>True when the item counts against a body capacity rule (two eyes, one Cyberaudio Suite...).</summary>
    public static bool MatchesCapacity(CapacityRule rule, CatalogItem item)
    {
        if (rule.Slugs.Length > 0) return rule.Slugs.Contains(item.Slug, StringComparer.OrdinalIgnoreCase);
        if (item.Cyber is not { } cy || item.Category != ItemCategory.Cyberware) return false;
        if (rule.FoundationalOnly && !cy.Foundational) return false;
        if (rule.BodyLocation is { } loc && cy.Location != loc) return false;
        return rule.CyberwareTypes.Length == 0 || (cy.Type is { } t && rule.CyberwareTypes.Contains(t));
    }

    /// <summary>Slot pool used by standalone cyberware (Internal, External, Fashionware: 7 slots each, CP:R p.111).</summary>
    public SlotPool? PoolFor(CatalogItem item)
    {
        if (item.Cyber is null || item.Cyber.Foundational) return null;
        return Rules.Document.Cyberware.Pools.FirstOrDefault(p =>
            item.Category == ItemCategory.Cyberware && item.Cyber.Type is { } t && p.CyberwareTypes.Contains(t));
    }

    /// <summary>
    /// Prerequisites that exist only as free text (anything beyond install location, pairing and the
    /// structured parent piece). The system cannot verify them, so they are surfaced as warnings.
    /// </summary>
    public string? FreeTextRequirement(CatalogItem item, NpcCatalog catalog)
    {
        var text = item.Cyber?.Requirements ?? item.Armor?.Requirements;
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parentName = item.Cyber?.RequiresItemId is { } pid ? catalog.Find(pid)?.Name : null;
        var parts = text.Split([';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => InstallNoise().Replace(p, "").Trim(' ', ',', '.'))
            .Where(p => p.Length > 0)
            .Where(p => parentName is null || !p.Equals(parentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    [GeneratedRegex(@"\b(Mall|Clinic|Clínica|Hospital|N/A)\b|\((Requer Par|par)\)", RegexOptions.IgnoreCase)]
    private static partial Regex InstallNoise();

    // ------------------------------------------------------------ derived values

    /// <summary>HP = 10 + 5 × ceil((BODY + WILL) / 2) (CP:R p.79).</summary>
    public int HitPoints(int body, int will) =>
        Rules.Document.Derived.HpBase.Value + Rules.Document.Derived.HpPerAverage.Value * (int)Math.Ceiling((body + will) / 2.0);

    public static int SeriouslyWounded(int hp) => (int)Math.Ceiling(hp / 2.0);
}
