using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;

namespace CyberpunkDb.Application.Npcs.Generation;

/// <summary>
/// Builds the cyberware section: keeps locked implants, installs the required ones (with the foundational
/// pieces they depend on), then adds optional implants that fit the profile. Never installs a forbidden item,
/// never exceeds slots, body capacity, exclusive groups or the budget.
/// </summary>
public sealed class CyberwarePlanner(NpcMechanics mechanics, NpcCatalog catalog)
{
    private readonly NpcRuleBook _rules = mechanics.Rules;
    private readonly List<NpcCyberwareEntry> _entries = [];
    private readonly HashSet<int> _forbidden = [];
    private int _nextId;

    public IReadOnlyList<NpcCyberwareEntry> Entries => _entries;
    public int Spent => _entries.Sum(e => catalog.Find(e.ItemId)?.Price ?? 0);

    public CyberwarePlanner WithForbidden(IEnumerable<int> itemIds)
    {
        _forbidden.UnionWith(itemIds);
        return this;
    }

    public void AddLocked(IEnumerable<NpcCyberwareEntry> locked) => AddExisting(locked.Select(e => e with { Locked = true }));

    /// <summary>Starts from implants already on the sheet, keeping their flags.</summary>
    public void AddExisting(IEnumerable<NpcCyberwareEntry> entries)
    {
        _entries.AddRange(entries);
        _nextId = _entries.Count;
    }

    /// <summary>Installs an item and any missing foundational pieces. Returns an explanation on failure.</summary>
    public string? TryInstall(CatalogItem item, int? budget)
    {
        if (_forbidden.Contains(item.Id)) return $"{item.Name} está proibido.";
        var plan = new List<NpcCyberwareEntry>();
        var error = Plan(item, plan, depth: 0);
        if (error is not null) return error;
        var cost = plan.Sum(e => catalog.Find(e.ItemId)?.Price ?? 0);
        if (budget is { } b && Spent + cost > b)
            return $"{item.Name} custa {NpcValidator.Eb(cost)} com as dependências; sobram só {NpcValidator.Eb(Math.Max(0, b - Spent))}.";
        _entries.AddRange(plan);
        return null;
    }

    public bool Contains(int itemId) => _entries.Any(e => e.ItemId == itemId);

    /// <summary>Adds optional implants weighted by the profile until the spending target is reached.</summary>
    public void FillOptional(NpcProfile profile, int? budget, int target, Random rng)
    {
        foreach (var slug in profile.SignatureCyberware)
            if (catalog.Find(slug) is { Cyber: not null } signature && !Contains(signature.Id) && AutoEligible(signature))
                TryInstall(signature, budget);

        var candidates = profile.Cyberware
            .Select(kv => (Item: catalog.Find(kv.Key), Weight: kv.Value))
            .Where(c => c.Item is { Cyber: not null, Price: not null } && !_forbidden.Contains(c.Item.Id) && AutoEligible(c.Item))
            .Select(c => (Item: c.Item!, c.Weight))
            .ToList();

        for (var guard = 0; guard < 60 && candidates.Count > 0 && Spent < target; guard++)
        {
            var pick = Weighted.Pick(candidates, c => c.Weight, rng);
            candidates.Remove(pick);
            if (Contains(pick.Item.Id)) continue;
            var limit = budget is { } b ? Math.Min(b, (int)(target * 1.15)) : (int)(target * 1.15);
            TryInstall(pick.Item, limit);
        }
    }

    /// <summary>Items whose prerequisites exist only as free text are never chosen automatically.</summary>
    public bool AutoEligible(CatalogItem item) =>
        !_rules.Document.Cyberware.AutoPickExcludesFreeTextRequirements.Value || mechanics.FreeTextRequirement(item, catalog) is null;

    // ------------------------------------------------------------ planning

    private string? Plan(CatalogItem item, List<NpcCyberwareEntry> plan, int depth)
    {
        if (depth > 4) return $"Cadeia de dependências de {item.Name} longa demais.";
        var cy = item.Cyber!;
        var current = _entries.Concat(plan).ToList();

        if (cy.RequiresItemId is not { } rid)
        {
            var standaloneError = CheckCapacity(item, current) ?? CheckPool(item, current);
            if (standaloneError is not null) return standaloneError;
            plan.Add(NewEntry(item, null));
            return null;
        }

        var required = catalog.Find(rid);
        if (required?.Cyber is null) return $"{item.Name} depende de uma peça que não está no catálogo.";
        var (slots, _) = mechanics.SlotsRequired(item);
        var copies = cy.RequiresPair ? 2 : 1;
        var hosts = new List<string>();

        foreach (var parent in current.Where(e => catalog.Find(e.ItemId) is { } p && NpcMechanics.ParentCompatible(p, required)))
        {
            if (hosts.Count == copies) break;
            if (current.Any(e => e.ParentInstanceId == parent.InstanceId && e.ItemId == item.Id)) continue;
            if (FreeSlots(parent, current) >= slots) hosts.Add(parent.InstanceId);
        }

        while (hosts.Count < copies)
        {
            if (_forbidden.Contains(required.Id))
                return $"{item.Name} depende de {required.Name}, que está proibido.";
            if ((mechanics.SlotsProvided(required) ?? 0) < slots)
                return $"{required.Name} não tem Option Slots suficientes para {item.Name}.";
            var before = plan.Count;
            var error = Plan(required, plan, depth + 1);
            if (error is not null) return $"{item.Name} precisa de {required.Name}: {error}";
            hosts.Add(plan.Skip(before).Last(e => e.ItemId == required.Id).InstanceId);
        }

        foreach (var host in hosts) plan.Add(NewEntry(item, host));
        return CheckCapacity(item, _entries.Concat(plan).ToList(), alreadyIncluded: true);
    }

    private int FreeSlots(NpcCyberwareEntry parent, List<NpcCyberwareEntry> current)
    {
        var provided = catalog.Find(parent.ItemId) is { } p ? mechanics.SlotsProvided(p) ?? 0 : 0;
        var used = current.Where(e => e.ParentInstanceId == parent.InstanceId)
            .Sum(e => catalog.Find(e.ItemId) is { } c ? mechanics.SlotsRequired(c).Slots : 1);
        return provided - used;
    }

    private string? CheckCapacity(CatalogItem item, List<NpcCyberwareEntry> current, bool alreadyIncluded = false)
    {
        var items = current.Select(e => catalog.Find(e.ItemId)).Where(i => i is not null).Select(i => i!).ToList();
        if (!alreadyIncluded) items.Add(item);

        foreach (var rule in _rules.Document.Cyberware.Capacity)
        {
            if (!NpcMechanics.MatchesCapacity(rule, item)) continue;
            var count = items.Count(i => NpcMechanics.MatchesCapacity(rule, i));
            if (count > rule.Max) return $"{rule.Label}: limite de {rule.Max}.";
        }
        foreach (var group in _rules.Document.Cyberware.ExclusiveGroups)
        {
            if (!group.Slugs.Contains(item.Slug, StringComparer.OrdinalIgnoreCase)) continue;
            if (items.Count(i => group.Slugs.Contains(i.Slug, StringComparer.OrdinalIgnoreCase)) > group.Max)
                return $"A combinação de implantes selecionada possui incompatibilidades: só {group.Max} {group.Label} por vez.";
        }
        return null;
    }

    private string? CheckPool(CatalogItem item, List<NpcCyberwareEntry> current)
    {
        if (mechanics.PoolFor(item) is not { } pool) return null;
        var used = current.Where(e => e.ParentInstanceId is null)
            .Select(e => catalog.Find(e.ItemId))
            .Where(i => i is not null && mechanics.PoolFor(i)?.Key == pool.Key)
            .Sum(i => mechanics.SlotsRequired(i!).Slots);
        return used + mechanics.SlotsRequired(item).Slots > pool.Slots ? $"{pool.Label} sem slots livres." : null;
    }

    private NpcCyberwareEntry NewEntry(CatalogItem item, string? parent)
    {
        string id;
        do id = $"cw{++_nextId}"; while (_entries.Any(e => e.InstanceId == id));
        return new NpcCyberwareEntry { InstanceId = id, ItemId = item.Id, ParentInstanceId = parent };
    }
}
