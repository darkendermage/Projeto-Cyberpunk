using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// Installing, removing and replacing cyberware, and Humanity therapy. Every check (money, slots, prerequisites,
/// body capacity, pairs, requirements) runs before anything changes; a refused operation leaves the sheet untouched.
/// </summary>
public sealed class CyberwareOperations(CharacterRuleBook rules, CyberwareInstaller installer, CharacterCalculator calculator, IDiceRoller dice)
{
    // ------------------------------------------------------------ preview

    /// <summary>What an installation would do, without doing it: pieces, price, budget after, Humanity range, warnings.</summary>
    public InstallPreviewDto Preview(CharacterSheet sheet, InstallCyberwareRequest request, NpcCatalog catalog)
    {
        if (request.Replacing is { } replaced)
        {
            var gone = Descendants(sheet, replaced).Select(c => c.InstanceId).Append(replaced).ToHashSet();
            sheet = sheet with { Cyberware = sheet.Cyberware.Where(c => !gone.Contains(c.InstanceId)).ToList() };
        }
        var report = calculator.Calculate(sheet, catalog);
        CatalogItem item;
        try
        {
            item = Resolve(sheet, request, catalog).Item;
        }
        catch (Common.RequestValidationException e)
        {
            return Refused(e.Errors.Values.SelectMany(v => v).First(), report);
        }

        var creating = !sheet.Creation.Finalized;
        var (plan, error) = installer.Plan(sheet, catalog, item, Options(request, creating, roll: false));
        if (plan is null) return Refused(error!, report);

        var pool = Payments.PoolFor(rules, sheet, plan.Pieces.All(p => CharacterValidator.IsFashion(catalog.Find(p.ItemId)!)), request.Pool);
        var price = Price(plan, item, request.FromInventory is not null);
        var available = Payments.Available(rules, sheet, pool);
        var after = calculator.Calculate(sheet with { Cyberware = [.. sheet.Cyberware, .. plan.Pieces] }, catalog).Humanity;
        var humanity = report.Humanity.Current;

        var warnings = new List<string>();
        if (humanity - plan.HumanityLossMax < 0) warnings.Add("No pior resultado dos dados a Humanidade fica abaixo de zero: cyberpsicose (CP:R p.80).");
        if (price > available) warnings.Add($"Falta {CharacterValidator.Eb(price - available)} para pagar.");
        if (plan.TextRequirements.Count > 0 && !request.Acknowledge)
            warnings.Add("Confirme os requisitos em texto: o sistema não consegue verificá-los.");

        var names = sheet.Cyberware.Concat(plan.Pieces).ToDictionary(p => p.InstanceId, p => catalog.Find(p.ItemId)?.Name ?? "?");
        var pieces = plan.Pieces.Select(p =>
        {
            var i = catalog.Find(p.ItemId)!;
            return new PreviewPieceDto(p.InstanceId, i.Name, i.Slug, i.Price ?? 0,
                p.HumanityLossApplied, creating ? null : i.Cyber?.HumanityLossDice,
                p.ParentInstanceId is { } parent ? names.GetValueOrDefault(parent) : null, p.Side, p.MeatLimb,
                CyberwareAnatomy.RegionOf(p, [.. sheet.Cyberware, .. plan.Pieces], catalog));
        }).ToList();

        return new InstallPreviewDto(
            price <= available, price > available ? $"Saldo insuficiente: custa {CharacterValidator.Eb(price)} e restam {CharacterValidator.Eb(available)}." : null,
            pieces, price, pool, available, available - price,
            humanity, plan.HumanityLossMin, plan.HumanityLossMax, !creating,
            report.Humanity.EmpEffective, Math.Max(0, humanity - plan.HumanityLossMax) / 10, after.TherapyCap,
            plan.TextRequirements, warnings);
    }

    private static InstallPreviewDto Refused(string error, CharacterReport report) =>
        new(false, error, [], 0, BudgetPool.Balance, 0, 0, report.Humanity.Current, 0, 0, false,
            report.Humanity.EmpEffective, report.Humanity.EmpEffective, report.Humanity.TherapyCap, [], []);

    // ------------------------------------------------------------ install

    /// <summary>
    /// Installs a piece bought now or taken from the inventory. Foundational pieces the planner adds are charged too;
    /// requirements that exist only as text must be confirmed. Humanity Loss is preset during creation and rolled after.
    /// </summary>
    public CharacterSheet Install(CharacterSheet sheet, InstallCyberwareRequest request, CharacterChange change)
    {
        var (item, fromInventory) = Resolve(sheet, request, change.Catalog);
        var creating = !sheet.Creation.Finalized;
        var (plan, error) = installer.Plan(sheet, change.Catalog, item, Options(request, creating, roll: true));
        if (plan is null) throw CharacterChange.Refuse("itemId", error!);
        if (plan.TextRequirements.Count > 0 && !request.Acknowledge)
            throw CharacterChange.Refuse("acknowledge", "Confirme que o personagem cumpre: " + string.Join("; ", plan.TextRequirements));

        var pool = Payments.PoolFor(rules, sheet, plan.Pieces.All(p => CharacterValidator.IsFashion(change.Item(p.ItemId))), request.Pool);
        var price = Price(plan, item, fromInventory is not null);
        sheet = Payments.Charge(rules, sheet, pool, price, item.Name, change);

        if (fromInventory is not null)
        {
            sheet = TakeFromInventory(sheet, fromInventory);
            change.Log(CharacterEventKind.Edit, "install-from-inventory", $"{item.Name} retirado do inventário para instalação.");
        }

        var ownedPiece = fromInventory is null ? null : plan.Pieces.FirstOrDefault(p => p.ItemId == item.Id)?.InstanceId;
        var pieces = plan.Pieces.Select(p => p with
        {
            Origin = p.InstanceId == ownedPiece ? fromInventory!.Origin : ItemOrigin.Purchase,
            PaidFrom = pool,
            PricePaid = p.InstanceId == ownedPiece ? 0 : change.Item(p.ItemId).Price ?? 0,
            InstalledAt = change.Now,
        }).ToList();
        foreach (var p in pieces)
        {
            var i = change.Item(p.ItemId);
            change.Log(CharacterEventKind.Humanity, "install-cyberware",
                $"{i.Name} instalado: Humanity Loss {p.HumanityLossApplied}" +
                (p.HumanityLossRolled ? $" (rolado {i.Cyber?.HumanityLossDice})." : creating ? " (pré-definida na criação)." : "."),
                -p.HumanityLossApplied);
        }
        if (plan.TextRequirements.Count > 0)
            change.Log(CharacterEventKind.Edit, "requirements-confirmed", "Requisitos confirmados pelo jogador: " + string.Join("; ", plan.TextRequirements), manual: true);
        if (calculator.Calculate(sheet with { Cyberware = [.. sheet.Cyberware, .. pieces], Humanity = sheet.Humanity with { Lost = sheet.Humanity.Lost + plan.HumanityLoss } }, change.Catalog)
                .Humanity.Cyberpsychosis)
            change.Notices.Add("A Humanidade caiu abaixo de zero: cyberpsicose (CP:R p.80).");

        return sheet with
        {
            Cyberware = [.. sheet.Cyberware, .. pieces],
            Humanity = sheet.Humanity with { Lost = sheet.Humanity.Lost + plan.HumanityLoss },
        };
    }

    // ------------------------------------------------------------ remove and replace

    /// <summary>
    /// Takes a piece out, with every option installed in it. The Humanity it cost does not come back: removing only
    /// raises the ceiling that therapy can restore (CP:R p.230). Removed pieces go back to the inventory.
    /// </summary>
    public CharacterSheet Remove(CharacterSheet sheet, RemoveCyberwareRequest request, CharacterChange change)
    {
        if (!sheet.Creation.Finalized) throw CharacterChange.Refuse("creation", "Durante a criação, desfaça a compra em vez de remover.");
        var piece = sheet.Cyberware.FirstOrDefault(c => c.InstanceId == request.InstanceId)
                    ?? throw CharacterChange.Refuse("instanceId", "Implante não encontrado.");
        var removed = Descendants(sheet, piece.InstanceId).Prepend(piece).ToList();
        var ids = removed.Select(r => r.InstanceId).ToHashSet();
        var capBefore = calculator.Calculate(sheet, change.Catalog).Humanity.TherapyCap;
        sheet = sheet with { Cyberware = sheet.Cyberware.Where(c => !ids.Contains(c.InstanceId)).ToList() };

        if (request.Keep && rules.Document.Cyberware.RemovedPiecesKept.Value)
        {
            var inventory = new List<InventoryEntry>(sheet.Inventory);
            foreach (var r in removed)
            {
                change.Sheet = sheet with { Inventory = inventory };
                inventory.Add(new InventoryEntry
                {
                    InstanceId = change.NewId("it"), ItemId = r.ItemId, Origin = ItemOrigin.Manual, PricePaid = 0, State = ItemState.Stored,
                    Notes = $"Removido em {change.Now:dd/MM/yyyy}",
                });
            }
            sheet = sheet with { Inventory = inventory };
        }

        var capAfter = calculator.Calculate(sheet, change.Catalog).Humanity.TherapyCap;
        change.Log(CharacterEventKind.Humanity, "remove-cyberware",
            $"Removido: {string.Join(", ", removed.Select(r => change.Item(r.ItemId).Name))}. A Humanidade perdida não volta; " +
            $"o teto da terapia sobe de {capBefore} para {capAfter} ({CharacterRuleBook.Page(rules.Document.Cyberware.RemovalRestoresHumanity.Reference)}).",
            0, capBefore.ToString(), capAfter.ToString());
        return sheet;
    }

    /// <summary>Removes one piece and installs another as a single operation: if the new one can't go in, nothing changes.</summary>
    public CharacterSheet Replace(CharacterSheet sheet, ReplaceCyberwareRequest request, CharacterChange change)
    {
        var removed = Remove(sheet, new RemoveCyberwareRequest(request.Version, request.InstanceId, request.Keep), change);
        change.Sheet = removed;
        return Install(removed, new InstallCyberwareRequest(request.Version, request.ItemId, null, request.MeatLimb, request.HostInstanceId,
            request.Acknowledge, Side: request.Side), change);
    }

    // ------------------------------------------------------------ therapy

    /// <summary>
    /// One week of therapy (CP:R p.229–230): recovers 2d6 or 4d6 Humanity, never above the ceiling set by the cyberware
    /// still installed. Paid at a clinic (full price), by a Medtech (materials only) or free by GM ruling.
    /// </summary>
    public CharacterSheet Therapy(CharacterSheet sheet, TherapyRequest request, CharacterChange change)
    {
        if (!sheet.Creation.Finalized) throw CharacterChange.Refuse("creation", "Conclua a criação antes de fazer terapia.");
        var kind = rules.Document.Therapy.Kinds.FirstOrDefault(k => k.Id.Equals(request.Kind, StringComparison.OrdinalIgnoreCase))
                   ?? throw CharacterChange.Refuse("kind", "Tipo de terapia desconhecido.");
        if (!rules.Document.Therapy.Payment.Value.Contains(request.Payment, StringComparer.OrdinalIgnoreCase))
            throw CharacterChange.Refuse("payment", "Forma de pagamento desconhecida.");

        var humanity = calculator.Calculate(sheet, change.Catalog).Humanity;
        var room = humanity.TherapyCap - humanity.Current;
        if (room <= 0)
            throw CharacterChange.Refuse("kind", $"A Humanidade ({humanity.Current}) já está no teto da terapia ({humanity.TherapyCap}). " +
                                                 "Só remover cyberware libera mais (CP:R p.230).");

        var cost = request.Payment.ToLowerInvariant() switch { "clinic" => kind.Cost, "medtech" => kind.Materials, _ => 0 };
        if (cost > 0) sheet = Payments.Charge(rules, sheet, BudgetPool.Balance, cost, $"terapia {kind.Label}", change);
        else change.Log(CharacterEventKind.Ledger, "therapy-free", $"Terapia {kind.Label} sem custo (decisão do Mestre).", 0, manual: true);

        var range = Dice.Range(kind.Recovery)!.Value;
        var roll = request.Roll ?? Dice.Roll(kind.Recovery, dice)!.Value;
        if (roll < range.Min || roll > range.Max)
            throw CharacterChange.Refuse("roll", $"{kind.Recovery} vai de {range.Min} a {range.Max}.");
        var gained = Math.Min(roll, room);
        change.Log(CharacterEventKind.Humanity, "therapy",
            $"Terapia {kind.Label} ({kind.Recovery} = {roll}): +{gained} Humanidade" +
            (gained < roll ? $", limitada pelo teto de {humanity.TherapyCap}" : "") + $" ({CharacterRuleBook.Page(kind.Source)}).",
            gained, humanity.Current.ToString(), (humanity.Current + gained).ToString(), manual: request.Roll is not null);
        return sheet with { Humanity = sheet.Humanity with { Recovered = sheet.Humanity.Recovered + gained } };
    }

    // ------------------------------------------------------------ helpers

    private (CatalogItem Item, InventoryEntry? FromInventory) Resolve(CharacterSheet sheet, InstallCyberwareRequest request, NpcCatalog catalog)
    {
        if (request.FromInventory is { } instance)
        {
            var entry = sheet.Inventory.FirstOrDefault(e => e.InstanceId == instance)
                        ?? throw CharacterChange.Refuse("fromInventory", "Item não encontrado no inventário.");
            var stored = catalog.Find(entry.ItemId) ?? throw CharacterChange.Refuse("fromInventory", "Item fora do catálogo.");
            if (stored.Cyber is null) throw CharacterChange.Refuse("fromInventory", $"{stored.Name} não é cyberware.");
            return (stored, entry);
        }
        var item = request.ItemId is { } id ? catalog.Find(id) : null;
        if (item?.Cyber is null) throw CharacterChange.Refuse("itemId", "Escolha um implante do catálogo.");
        if (item.Price is null) throw CharacterChange.Refuse("itemId", $"{item.Name} não tem preço de catálogo.");
        return (item, null);
    }

    private static InstallOptions Options(InstallCyberwareRequest r, bool creating, bool roll) =>
        new() { MeatLimb = r.MeatLimb, HostInstanceId = r.HostInstanceId, AtCreation = creating, Roll = roll, Side = r.Side };

    /// <summary>
    /// The plan's price. A piece taken from the inventory is already owned, so one copy of it is not charged again
    /// (a pair still pays for the second copy).
    /// </summary>
    private static int Price(InstallPlan plan, CatalogItem item, bool fromInventory) =>
        Math.Max(0, plan.Price - (fromInventory ? item.Price ?? 0 : 0));

    private static CharacterSheet TakeFromInventory(CharacterSheet sheet, InventoryEntry entry) =>
        sheet with
        {
            Inventory = entry.Quantity > 1
                ? sheet.Inventory.Select(e => e.InstanceId == entry.InstanceId ? e with { Quantity = e.Quantity - 1 } : e).ToList()
                : sheet.Inventory.Where(e => e.InstanceId != entry.InstanceId).ToList(),
        };

    private static IEnumerable<CharacterCyberware> Descendants(CharacterSheet sheet, string id) =>
        sheet.Cyberware.Where(c => c.ParentInstanceId == id).SelectMany(c => Descendants(sheet, c.InstanceId).Prepend(c));
}
