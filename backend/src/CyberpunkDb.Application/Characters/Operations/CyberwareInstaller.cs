using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

public sealed record InstallOptions
{
    // CP:R p.115-116
    public bool MeatLimb { get; init; }
    // for coverings and Hardened Shielding
    public string? HostInstanceId { get; init; }
    // preset HL (CP:R p.110)
    public bool AtCreation { get; init; }
    // previews leave it off and show the range
    public bool Roll { get; init; } = true;
    public BodySide? Side { get; init; }
}

// Not applied yet. Includes the HL range and the text-only requirements the player has to confirm.
public sealed record InstallPlan(
    IReadOnlyList<CharacterCyberware> Pieces, int Price, int HumanityLoss, int HumanityLossMin, int HumanityLossMax,
    IReadOnlyList<string> TextRequirements);

// Same planner as the NPC generator, plus PC-only stuff: meat limbs, limb options, body sides and the FBC
// exceptions (Interface RED 3 p.93).
public sealed class CyberwareInstaller(CharacterRuleBook rules, IDiceRoller dice)
{
    public NpcMechanics Mechanics(CharacterSheet sheet, NpcCatalog catalog) =>
        new(rules.Shared, rules.InFbc(sheet, catalog) ? rules.Document.Cyberware.Fbc.Value.SlotMultiplier : 1);

    public (InstallPlan? Plan, string? Error) Plan(CharacterSheet sheet, NpcCatalog catalog, CatalogItem item, InstallOptions options)
    {
        if (item.Cyber is null) return (null, L.T($"{item.Name} não é cyberware.", $"{item.Name} isn't cyberware."));
        var mechanics = Mechanics(sheet, catalog);

        var (added, error) = rules.FindAnyLimbOption(item.Slug) is { } anyLimb
            ? OnLimb(sheet, catalog, mechanics, item, anyLimb, options.HostInstanceId, options.Side)
            : options.MeatLimb
                ? OnMeatLimb(sheet, catalog, item, options.Side)
                : WithPlanner(sheet, catalog, mechanics, item, options.Side);
        if (error is not null) return (null, error);

        added = AssignSides(sheet, catalog, added!, options.Side);

        // Listed foundations cost no Humanity in an FBC body (IR3 p.93), including when the body is part of this install.
        var fbc = rules.InFbc(sheet, catalog) || added.Any(p => catalog.Find(p.ItemId) is { } i && rules.IsFbcBody(i));
        var pieces = new List<CharacterCyberware>();
        int min = 0, max = 0;
        foreach (var p in added)
        {
            var piece = catalog.Find(p.ItemId)!;
            var fixedLoss = piece.Cyber?.HumanityLoss ?? 0;
            var expression = piece.Cyber?.HumanityLossDice;
            if (fbc && rules.IsFbcExempt(piece.Slug))
            {
                pieces.Add(p with { HumanityLossApplied = 0 });
                continue;
            }
            // preset at creation, rolled afterwards (CP:R p.110)
            var range = options.AtCreation ? null : Dice.Range(expression);
            min += range?.Min ?? fixedLoss;
            max += range?.Max ?? fixedLoss;
            var rolled = !options.AtCreation && options.Roll ? Dice.Roll(expression, dice) : null;
            pieces.Add(p with { HumanityLossApplied = rolled ?? fixedLoss, HumanityLossRolled = rolled is not null });
        }

        var requirements = pieces
            .Select(p => catalog.Find(p.ItemId)!)
            .Select(i => (i.Name, Text: mechanics.FreeTextRequirement(i, catalog)))
            .Where(x => x.Text is not null && !(fbc && x.Text!.Contains("Full Body Conversion", StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Name}: {x.Text}")
            .ToList();
        return (new InstallPlan(pieces, pieces.Sum(p => catalog.Find(p.ItemId)?.Price ?? 0), pieces.Sum(p => p.HumanityLossApplied),
            min, max, requirements), null);
    }

    // placement strategies

    private static (List<CharacterCyberware>?, string?) WithPlanner(
        CharacterSheet sheet, NpcCatalog catalog, NpcMechanics mechanics, CatalogItem item, BodySide? side)
    {
        // the planner takes the first host with free slots, so put the chosen side first
        var ordered = side is null ? sheet.Cyberware : sheet.Cyberware.OrderBy(c => c.Side == side ? 0 : 1).ToList();
        var planner = new CyberwarePlanner(mechanics, catalog);
        planner.AddExisting(ordered.Select(c => new NpcCyberwareEntry { InstanceId = c.InstanceId, ItemId = c.ItemId, ParentInstanceId = c.ParentInstanceId }));
        var before = sheet.Cyberware.Count;
        var error = planner.TryInstall(item, budget: null);
        if (error is not null) return (null, error);
        return (planner.Entries.Skip(before)
            .Select(e => new CharacterCyberware { InstanceId = e.InstanceId, ItemId = e.ItemId, ParentInstanceId = e.ParentInstanceId })
            .ToList(), null);
    }

    private (List<CharacterCyberware>?, string?) OnMeatLimb(CharacterSheet sheet, NpcCatalog catalog, CatalogItem item, BodySide? preferred)
    {
        var leg = rules.IsMeatLegOption(item.Slug);
        if (!leg && !rules.IsMeatArmOption(item.Slug))
            return (null, L.T($"{item.Name} não pode ser instalado num membro de carne ({CharacterRuleBook.Page(rules.Document.Cyberware.MeatArmOptions.Reference)}).", $"{item.Name} can't be installed in a meat limb ({CharacterRuleBook.Page(rules.Document.Cyberware.MeatArmOptions.Reference)})."));
        var limb = leg ? CyberwareAnatomy.Limb.Leg : CyberwareAnatomy.Limb.Arm;
        var side = CyberwareAnatomy.FreeSide(sheet.Cyberware, catalog, limb, preferred);
        if (side is null)
            return (null, L.T($"Não há {(leg ? "perna" : "braço")} de carne livre: cada um aceita uma única peça de cyberware.", $"There's no free meat {(leg ? "leg" : "arm")}: each takes a single piece of cyberware."));
        return ([new CharacterCyberware { InstanceId = NextId(sheet), ItemId = item.Id, MeatLimb = true, Side = side }], null);
    }

    // coverings take no slot (CP:R p.116)
    private (List<CharacterCyberware>?, string?) OnLimb(
        CharacterSheet sheet, NpcCatalog catalog, NpcMechanics mechanics, CatalogItem item, AnyLimbOption option, string? hostId, BodySide? preferred)
    {
        var limbs = sheet.Cyberware
            .Where(c => catalog.Find(c.ItemId)?.Cyber is { Type: CyberwareType.Cyberlimb, Foundational: true })
            .ToList();
        if (limbs.Count == 0) return (null, L.T($"{item.Name} precisa de um Cyberarm ou Cyberleg instalado (CP:R p.116).", $"{item.Name} needs an installed Cyberarm or Cyberleg (CP:R p.116)."));

        var slots = option.Covering ? 0 : mechanics.SlotsRequired(item).Slots;
        string? Problem(CharacterCyberware host)
        {
            var children = sheet.Cyberware.Where(c => c.ParentInstanceId == host.InstanceId).ToList();
            if (children.Any(c => c.ItemId == item.Id)) return L.T($"{item.Name} já está nesse membro.", $"{item.Name} is already on that limb.");
            if (option.Covering && rules.Document.Cyberware.OneCoveringPerLimb.Value
                && children.Any(c => catalog.Find(c.ItemId) is { } ci && rules.FindAnyLimbOption(ci.Slug)?.Covering == true))
                return L.T("Esse membro já tem uma cobertura.", "That limb already has a covering.");
            var free = (mechanics.SlotsProvided(catalog.Find(host.ItemId)!) ?? 0)
                       - children.Sum(c => catalog.Find(c.ItemId) is { } ci ? (rules.FindAnyLimbOption(ci.Slug)?.Covering == true ? 0 : mechanics.SlotsRequired(ci).Slots) : 1);
            return free < slots ? L.T("Esse membro não tem Option Slots livres.", "That limb has no free Option Slots.") : null;
        }

        CharacterCyberware? chosen;
        if (hostId is null && preferred is not null)
            hostId = limbs.FirstOrDefault(l => l.Side == preferred && Problem(l) is null)?.InstanceId;
        if (hostId is not null)
        {
            chosen = limbs.FirstOrDefault(l => l.InstanceId == hostId);
            if (chosen is null) return (null, L.T("Escolha um Cyberarm ou Cyberleg instalado.", "Pick an installed Cyberarm or Cyberleg."));
            if (Problem(chosen) is { } p) return (null, p);
        }
        else
        {
            chosen = limbs.FirstOrDefault(l => Problem(l) is null);
            if (chosen is null) return (null, Problem(limbs[0]));
        }
        return ([new CharacterCyberware { InstanceId = NextId(sheet), ItemId = item.Id, ParentInstanceId = chosen.InstanceId, Side = chosen.Side }], null);
    }

    // options follow their host's side
    private static List<CharacterCyberware> AssignSides(CharacterSheet sheet, NpcCatalog catalog, List<CharacterCyberware> added, BodySide? preferred)
    {
        var placed = new List<CharacterCyberware>(sheet.Cyberware);
        var result = new List<CharacterCyberware>();
        foreach (var piece in added)
        {
            var item = catalog.Find(piece.ItemId);
            var next = piece;
            if (piece.Side is null && item is not null && CyberwareAnatomy.LimbOf(item, piece.MeatLimb) is { } limb)
                next = piece with { Side = CyberwareAnatomy.FreeSide(placed, catalog, limb, preferred) };
            else if (piece.Side is null && piece.ParentInstanceId is { } parent)
                next = piece with { Side = placed.FirstOrDefault(p => p.InstanceId == parent)?.Side };
            placed.Add(next);
            result.Add(next);
        }
        return result;
    }

    private static string NextId(CharacterSheet sheet)
    {
        var used = sheet.Cyberware.Select(c => c.InstanceId).ToHashSet(StringComparer.Ordinal);
        for (var n = sheet.Cyberware.Count + 1; ; n++)
            if (!used.Contains($"cw{n}")) return $"cw{n}";
    }
}
