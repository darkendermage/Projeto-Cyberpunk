using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// Options in worn hosts (Smart Glasses, Smart Lens, Battleglove, Smart Gloves, Smart Ears). Like weapon attachments,
/// the option is an inventory instance linked to the host: nothing is implanted, so no Humanity is lost
/// (<c>wearableHumanityLoss</c>), and the host needs no Cybereye or Cyberarm.
/// </summary>
public sealed class WearableOperations(CharacterRuleBook rules, InventoryOperations inventory)
{
    /// <summary>
    /// Puts an option in a host: one bought now (<see cref="WearableInstallRequest.ItemId"/>) or one already in the
    /// inventory. It costs what installing it once in a Cybereye or Cyberarm costs (CP:R p.347, 354).
    /// </summary>
    public CharacterSheet Install(CharacterSheet sheet, WearableInstallRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var hostItem = change.Item(hostEntry.ItemId);
        var host = rules.FindWearable(hostItem.Slug)
                   ?? throw CharacterChange.Refuse("wearableInstanceId", $"{hostItem.Name} não tem Option Slots.");
        if (request.ItemId is null == request.OptionInstanceId is null)
            throw CharacterChange.Refuse("itemId", "Escolha uma Option para comprar ou uma do inventário.");

        var option = request.ItemId is { } itemId ? change.Item(itemId) : change.Item(Find(sheet, request.OptionInstanceId!).ItemId);
        if (WearableCyberware.Incompatible(rules, host, hostItem.Name, option, change.Catalog) is { } problem)
            throw CharacterChange.Refuse("itemId", problem);
        var installed = WearableCyberware.OptionItems(sheet, change.Catalog, hostEntry).ToList();
        if (installed.Any(i => i.Id == option.Id) || host.BuiltIn.Contains(option.Slug, StringComparer.OrdinalIgnoreCase))
            throw CharacterChange.Refuse("itemId", $"{hostItem.Name} já tem {option.Name}.");
        var (slots, fingers) = WearableCyberware.Used(rules, change.Catalog, host, [.. installed, option]);
        if (fingers > host.Cyberfingers && option.Cyber?.Type == CyberwareType.Cyberfinger)
            throw CharacterChange.Refuse("itemId", $"{hostItem.Name} recebe até {host.Cyberfingers} Cyberfingers ({CharacterRuleBook.Page(host.Source)}).");
        if (slots > host.Slots)
            throw CharacterChange.Refuse("itemId",
                $"{hostItem.Name} tem {host.Slots} Option Slots e {option.Name} não cabe nos que sobram ({CharacterRuleBook.Page(host.Source)}).");

        InventoryEntry entry;
        if (request.ItemId is not null)
        {
            // Bought into the inventory first, so money, budget and history follow the normal purchase.
            var before = sheet.Inventory.Select(e => e.InstanceId).ToHashSet();
            sheet = inventory.Purchase(sheet, new PurchaseRequest(request.Version, option.Id, 1, Install: false, Pool: request.Pool), change);
            change.Sheet = sheet;
            entry = sheet.Inventory.FirstOrDefault(e => !before.Contains(e.InstanceId))
                    ?? sheet.Inventory.Last(e => e.ItemId == option.Id && e.State != ItemState.Equipped);
        }
        else entry = Find(sheet, request.OptionInstanceId!);
        if (Holder(sheet, entry.InstanceId) is { } other && entry.Quantity <= 1)
            throw CharacterChange.Refuse("optionInstanceId", $"{option.Name} já está em {change.Item(other.ItemId).Name}.");

        // One unit of a stack is split off, so the rest stays available.
        if (entry.Quantity > 1)
        {
            sheet = Replace(sheet, entry with { Quantity = entry.Quantity - 1 });
            change.Sheet = sheet;
            var single = entry with { InstanceId = change.NewId("it"), Quantity = 1, PricePaid = null };
            sheet = sheet with { Inventory = [.. sheet.Inventory, single] };
            entry = single;
        }
        sheet = Replace(sheet, entry with { State = ItemState.Equipped });
        hostEntry = Find(sheet, hostEntry.InstanceId);
        var state = hostEntry.Wearable ?? new WearableState();
        change.Log(CharacterEventKind.Edit, "wearable-install",
            $"{option.Name} instalado em {hostItem.Name}: vestível, sem Humanity Loss ({rules.Document.Cyberware.WearableHumanityLoss.Reference}).");
        return Replace(sheet, hostEntry with { Wearable = state with { Options = [.. state.Options, entry.InstanceId] } });
    }

    /// <summary>Takes an option out of its host; it stays in the inventory.</summary>
    public CharacterSheet Remove(CharacterSheet sheet, WearableOptionRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var state = hostEntry.Wearable ?? new WearableState();
        if (!state.Options.Contains(request.OptionInstanceId))
            throw CharacterChange.Refuse("optionInstanceId", "Essa Option não está nesse item.");
        var entry = Find(sheet, request.OptionInstanceId);
        change.Log(CharacterEventKind.Edit, "wearable-remove", $"{change.Item(entry.ItemId).Name} retirado de {change.Item(hostEntry.ItemId).Name}.");
        sheet = Replace(sheet, entry with { State = ItemState.Carried });
        return Replace(sheet, Find(sheet, hostEntry.InstanceId) with
        {
            Wearable = state with { Options = state.Options.Where(o => o != request.OptionInstanceId).ToList() },
        });
    }

    /// <summary>The hand a glove is worn on: the options of the arm under it stop working while it is worn.</summary>
    public CharacterSheet SetSide(CharacterSheet sheet, WearableSideRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var item = change.Item(hostEntry.ItemId);
        if (rules.FindWearable(item.Slug) is not { CoversArm: true })
            throw CharacterChange.Refuse("wearableInstanceId", $"{item.Name} não é usado numa mão.");
        return Replace(sheet, hostEntry with { Wearable = (hostEntry.Wearable ?? new WearableState()) with { Side = request.Side } });
    }

    /// <summary>
    /// Drops the links of an inventory instance that leaves the sheet: from its host when it is an option, and the
    /// options of a host go back to being carried.
    /// </summary>
    public static List<InventoryEntry> Unlink(List<InventoryEntry> items, string instanceId)
    {
        var options = items.FirstOrDefault(e => e.InstanceId == instanceId)?.Wearable?.Options ?? [];
        return items.Select(e =>
        {
            if (options.Contains(e.InstanceId)) return e with { State = ItemState.Carried };
            if (e.Wearable is { } w && w.Options.Contains(instanceId))
                return e with { Wearable = w with { Options = w.Options.Where(o => o != instanceId).ToList() } };
            return e;
        }).ToList();
    }

    private static InventoryEntry? Holder(CharacterSheet sheet, string optionInstanceId) =>
        sheet.Inventory.FirstOrDefault(e => e.Wearable?.Options.Contains(optionInstanceId) == true);

    private static InventoryEntry Find(CharacterSheet sheet, string instanceId) =>
        sheet.Inventory.FirstOrDefault(e => e.InstanceId == instanceId) ?? throw CharacterChange.Refuse("instanceId", "Item não encontrado no inventário.");

    private static CharacterSheet Replace(CharacterSheet sheet, InventoryEntry entry) =>
        sheet with { Inventory = sheet.Inventory.Select(e => e.InstanceId == entry.InstanceId ? entry : e).ToList() };
}
