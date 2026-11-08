using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

// Smart Glasses, Smart Lens, Battleglove, Smart Gloves, Smart Ears. The option is an inventory instance linked to
// the host, like an attachment: nothing implanted, no Humanity lost, no Cybereye/Cyberarm needed.
public sealed class WearableOperations(CharacterRuleBook rules, InventoryOperations inventory)
{
    // Costs the same as installing it once in a Cybereye or Cyberarm (CP:R p.347, 354).
    public CharacterSheet Install(CharacterSheet sheet, WearableInstallRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var hostItem = change.Item(hostEntry.ItemId);
        var host = rules.FindWearable(hostItem.Slug)
                   ?? throw CharacterChange.Refuse("wearableInstanceId", L.T($"{hostItem.Name} não tem Option Slots.", $"{hostItem.Name} has no Option Slots."));
        if (request.ItemId is null == request.OptionInstanceId is null)
            throw CharacterChange.Refuse("itemId", L.T("Escolha uma Option para comprar ou uma do inventário.", "Pick an Option to buy or one from the inventory."));

        var option = request.ItemId is { } itemId ? change.Item(itemId) : change.Item(Find(sheet, request.OptionInstanceId!).ItemId);
        if (WearableCyberware.Incompatible(rules, host, hostItem.Name, option, change.Catalog) is { } problem)
            throw CharacterChange.Refuse("itemId", problem);
        var installed = WearableCyberware.OptionItems(sheet, change.Catalog, hostEntry).ToList();
        if (installed.Any(i => i.Id == option.Id) || host.BuiltIn.Contains(option.Slug, StringComparer.OrdinalIgnoreCase))
            throw CharacterChange.Refuse("itemId", L.T($"{hostItem.Name} já tem {option.Name}.", $"{hostItem.Name} already has {option.Name}."));
        var (slots, fingers) = WearableCyberware.Used(rules, change.Catalog, host, [.. installed, option]);
        if (fingers > host.Cyberfingers && option.Cyber?.Type == CyberwareType.Cyberfinger)
            throw CharacterChange.Refuse("itemId", L.T($"{hostItem.Name} recebe até {host.Cyberfingers} Cyberfingers ({CharacterRuleBook.Page(host.Source)}).", $"{hostItem.Name} takes up to {host.Cyberfingers} Cyberfingers ({CharacterRuleBook.Page(host.Source)})."));
        if (slots > host.Slots)
            throw CharacterChange.Refuse("itemId",
                L.T($"{hostItem.Name} tem {host.Slots} Option Slots e {option.Name} não cabe nos que sobram ({CharacterRuleBook.Page(host.Source)}).", $"{hostItem.Name} has {host.Slots} Option Slots and {option.Name} doesn't fit in the ones left ({CharacterRuleBook.Page(host.Source)})."));

        InventoryEntry entry;
        if (request.ItemId is not null)
        {
            // buy into the inventory first so money/budget/history work like a normal purchase
            var before = sheet.Inventory.Select(e => e.InstanceId).ToHashSet();
            sheet = inventory.Purchase(sheet, new PurchaseRequest(request.Version, option.Id, 1, Install: false, Pool: request.Pool), change);
            change.Sheet = sheet;
            entry = sheet.Inventory.FirstOrDefault(e => !before.Contains(e.InstanceId))
                    ?? sheet.Inventory.Last(e => e.ItemId == option.Id && e.State != ItemState.Equipped);
        }
        else entry = Find(sheet, request.OptionInstanceId!);
        if (Holder(sheet, entry.InstanceId) is { } other && entry.Quantity <= 1)
            throw CharacterChange.Refuse("optionInstanceId", L.T($"{option.Name} já está em {change.Item(other.ItemId).Name}.", $"{option.Name} is already in {change.Item(other.ItemId).Name}."));

        // split one off the stack
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
            L.T($"{option.Name} instalado em {hostItem.Name}: vestível, sem Humanity Loss ({rules.Document.Cyberware.WearableHumanityLoss.Reference}).", $"{option.Name} installed in {hostItem.Name}: wearable, no Humanity Loss ({rules.Document.Cyberware.WearableHumanityLoss.Reference})."));
        return Replace(sheet, hostEntry with { Wearable = state with { Options = [.. state.Options, entry.InstanceId] } });
    }

    // stays in the inventory
    public CharacterSheet Remove(CharacterSheet sheet, WearableOptionRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var state = hostEntry.Wearable ?? new WearableState();
        if (!state.Options.Contains(request.OptionInstanceId))
            throw CharacterChange.Refuse("optionInstanceId", L.T("Essa Option não está nesse item.", "That Option isn't in that item."));
        var entry = Find(sheet, request.OptionInstanceId);
        change.Log(CharacterEventKind.Edit, "wearable-remove", L.T($"{change.Item(entry.ItemId).Name} retirado de {change.Item(hostEntry.ItemId).Name}.", $"{change.Item(entry.ItemId).Name} taken out of {change.Item(hostEntry.ItemId).Name}."));
        sheet = Replace(sheet, entry with { State = ItemState.Carried });
        return Replace(sheet, Find(sheet, hostEntry.InstanceId) with
        {
            Wearable = state with { Options = state.Options.Where(o => o != request.OptionInstanceId).ToList() },
        });
    }

    // options on the arm under the glove stop working
    public CharacterSheet SetSide(CharacterSheet sheet, WearableSideRequest request, CharacterChange change)
    {
        var hostEntry = Find(sheet, request.WearableInstanceId);
        var item = change.Item(hostEntry.ItemId);
        if (rules.FindWearable(item.Slug) is not { CoversArm: true })
            throw CharacterChange.Refuse("wearableInstanceId", L.T($"{item.Name} não é usado numa mão.", $"{item.Name} isn't used in a hand."));
        return Replace(sheet, hostEntry with { Wearable = (hostEntry.Wearable ?? new WearableState()) with { Side = request.Side } });
    }

    // An item leaving the sheet: unlink it from its host, and a host's options go back to being carried.
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
        sheet.Inventory.FirstOrDefault(e => e.InstanceId == instanceId) ?? throw CharacterChange.Refuse("instanceId", L.T("Item não encontrado no inventário.", "Item not found in the inventory."));

    private static CharacterSheet Replace(CharacterSheet sheet, InventoryEntry entry) =>
        sheet with { Inventory = sheet.Inventory.Select(e => e.InstanceId == entry.InstanceId ? entry : e).ToList() };
}
