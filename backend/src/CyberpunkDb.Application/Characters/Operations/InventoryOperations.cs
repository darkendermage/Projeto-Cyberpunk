using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// Buying, equipping, firing and reloading. A purchase checks the price, the paying budget and (for cyberware) the
/// installation before anything changes, then updates money, inventory and history together.
/// </summary>
public sealed class InventoryOperations(CharacterRuleBook rules, CyberwareOperations cyberware)
{
    // ------------------------------------------------------------ purchases

    /// <summary>
    /// Buys an item with the balance or, during a Complete Package, the creation budgets. Nothing is equipped or
    /// installed unless the player asks for it: cyberware is installed on purchase only during creation (the default
    /// there) or when <see cref="PurchaseRequest.Install"/> is set, and gear is equipped only with <see cref="PurchaseRequest.Equip"/>.
    /// </summary>
    public CharacterSheet Purchase(CharacterSheet sheet, PurchaseRequest request, CharacterChange change)
    {
        var item = change.Item(request.ItemId);
        var received = request.Received;
        if (received is { } how)
        {
            // Swapped between players, a gift or loot: no money moves. Creation budgets can't be bypassed this way.
            if (how is not (ItemOrigin.Trade or ItemOrigin.Gift or ItemOrigin.Loot))
                throw CharacterChange.Refuse("received", L.T("Sem custo, o item vem de troca, presente ou saque.", "At no cost, the item comes from a trade, gift or loot."));
            if (!sheet.Creation.Finalized)
                throw CharacterChange.Refuse("received", L.T("Itens sem custo só entram depois da criação: na criação o equipamento sai do orçamento.", "Free items only come in after creation: during creation gear comes out of the budget."));
        }
        else if (item.Price is null) throw CharacterChange.Refuse("itemId", L.T($"{item.Name} não tem preço de catálogo.", $"{item.Name} has no catalog price."));
        if (request.Quantity < 1 || request.Quantity > 999) throw CharacterChange.Refuse("quantity", L.T("Quantidade inválida.", "Invalid quantity."));

        var creating = !sheet.Creation.Finalized;
        var install = item.Cyber is not null && item.Ammo is null && (request.Install ?? creating);
        if (install && received is null)
            return cyberware.Install(sheet, new InstallCyberwareRequest(request.Version, item.Id, null, request.MeatLimb,
                request.HostInstanceId, request.Acknowledge, request.Pool), change);

        var pool = received is null ? Payments.PoolFor(rules, sheet, CharacterValidator.IsFashion(item), request.Pool) : BudgetPool.Balance;

        Caliber? caliber = null;
        int? rounds = null;
        if (item.Ammo is { } ammo)
        {
            caliber = request.Caliber ?? (ammo.Calibers.Count == 1 ? ammo.Calibers[0] : null);
            if (ammo.Calibers.Count > 1 && (caliber is null || !ammo.Calibers.Contains(caliber.Value)))
                throw CharacterChange.Refuse("caliber", L.T($"Escolha o calibre de {item.Name}.", $"Pick the caliber for {item.Name}."));
            rounds = ammo.Kind == AmmoKind.Round ? (ammo.PerPack ?? 1) * request.Quantity : null;
        }

        ArmorSlot? worn = null;
        var equip = request.Equip && (item.Weapon is not null || item.Armor is not null);
        if (equip && item.Armor is { Type: ArmorType.BodyArmor })
            worn = request.Worn ?? throw CharacterChange.Refuse("worn", L.T("Escolha onde vestir a armadura: cabeça ou corpo.", "Pick where to wear the armor: head or body."));

        var what = request.Quantity > 1 ? $"{item.Name} x{request.Quantity}" : item.Name;
        var origin = received ?? ItemOrigin.Purchase;
        var cost = received is null ? item.Price!.Value * request.Quantity : 0;
        if (received is null) sheet = Payments.Charge(rules, sheet, pool, cost, what, change);
        else
            change.Log(CharacterEventKind.Ledger, "receive", L.T($"{OriginLabel(origin)}: {what}, sem custo", $"{OriginLabel(origin)}: {what}, free") +
                                                             (request.Note is { Length: > 0 } n ? $" ({n.Trim()})." : "."), 0, manual: true);

        var inventory = new List<InventoryEntry>(sheet.Inventory);
        var stack = equip || install ? -1 : inventory.FindIndex(e => e.ItemId == item.Id && e.PaidFrom == pool && e.Origin == origin && e.State != ItemState.Equipped
                                                          && e.Caliber == caliber && item.Weapon is null && item.Armor is null && item.Attachment is null);
        if (stack >= 0)
        {
            var e = inventory[stack];
            inventory[stack] = e with
            {
                Quantity = rounds is null ? e.Quantity + request.Quantity : 1,
                Rounds = rounds is null ? e.Rounds : (e.Rounds ?? 0) + rounds,
                PricePaid = (e.PricePaid ?? 0) + cost,
            };
        }
        else
        {
            change.Sheet = sheet with { Inventory = inventory };
            var instanceId = change.NewId("it");
            inventory.Add(new InventoryEntry
            {
                InstanceId = instanceId,
                ItemId = item.Id,
                Quantity = rounds is null ? request.Quantity : 1,
                Caliber = caliber,
                Rounds = rounds,
                State = equip ? ItemState.Equipped : ItemState.Carried,
                Worn = worn,
                Origin = origin,
                PaidFrom = pool,
                PricePaid = cost,
                Notes = received is not null && request.Note?.Trim() is { Length: > 0 } note ? note : null,
            });
            // Received cyberware goes through the inventory: the piece itself is free, its Humanity Loss and any
            // foundational piece it needs are not.
            if (install)
                return cyberware.Install(sheet with { Inventory = inventory }, new InstallCyberwareRequest(request.Version, null, instanceId,
                    request.MeatLimb, request.HostInstanceId, request.Acknowledge, request.Pool), change);
        }
        if (equip) change.Log(CharacterEventKind.Edit, "equip", $"{item.Name} equipado {(received is null ? "na compra" : "ao receber")}.");
        return sheet with { Inventory = inventory };
    }

    private static string OriginLabel(ItemOrigin origin) => origin switch
    {
        ItemOrigin.Trade => L.T("Recebido em troca", "Received in a trade"),
        ItemOrigin.Gift => L.T("Recebido de presente", "Received as a gift"),
        ItemOrigin.Loot => "Saque",
        _ => "Recebido",
    };

    // ------------------------------------------------------------ weapon attachments

    /// <summary>
    /// Puts an attachment on a weapon: the weapon class must be listed by the attachment, Exotic weapons only take the
    /// ones that allow them, and non-Exotic ranged weapons have three Attachment Slots (CP:R p.95).
    /// </summary>
    public CharacterSheet Attach(CharacterSheet sheet, AttachRequest request, CharacterChange change)
    {
        var weaponEntry = Find(sheet, request.WeaponInstanceId);
        var weapon = change.Item(weaponEntry.ItemId);
        if (weapon.Weapon is not { } w) throw CharacterChange.Refuse("weaponInstanceId", L.T($"{weapon.Name} não é uma arma.", $"{weapon.Name} isn't a weapon."));
        var entry = Find(sheet, request.AttachmentInstanceId);
        var item = change.Item(entry.ItemId);
        if (item.Attachment is not { } a) throw CharacterChange.Refuse("attachmentInstanceId", L.T($"{item.Name} não é um acessório de arma.", $"{item.Name} isn't a weapon attachment."));
        if (entry.Quantity <= 1 && sheet.Inventory.Any(e => e.Weapon?.Attachments.Contains(entry.InstanceId) == true))
            throw CharacterChange.Refuse("attachmentInstanceId", L.T($"{item.Name} já está instalado em outra arma.", $"{item.Name} is already installed on another weapon."));

        if (a.Classes.Count > 0 && !a.Classes.Contains(w.Class))
            throw CharacterChange.Refuse("attachmentInstanceId", L.T($"{item.Name} não serve em {weapon.Name}: só em {string.Join(", ", a.Classes)}.", $"{item.Name} doesn't fit {weapon.Name}: only {string.Join(", ", a.Classes)}."));
        if (w.Exotic && !a.ExoticAllowed)
            throw CharacterChange.Refuse("attachmentInstanceId", L.T($"{weapon.Name} é exótica e {item.Name} não pode ser instalado nela.", $"{weapon.Name} is exotic and {item.Name} can't be installed on it."));

        var state = weaponEntry.Weapon ?? new WeaponState();
        var installed = state.Attachments.Select(id => sheet.Inventory.FirstOrDefault(e => e.InstanceId == id))
            .Where(e => e is not null).Select(e => change.Item(e!.ItemId)).ToList();
        if (installed.Any(i => i.Id == item.Id))
            throw CharacterChange.Refuse("attachmentInstanceId", L.T($"{weapon.Name} já tem {item.Name}.", $"{weapon.Name} already has {item.Name}."));
        var capacity = w.Calibers.Count > 0 && !w.Exotic ? rules.Document.Combat.AttachmentSlots.Value : 0;
        var used = installed.Sum(i => i.Attachment?.SlotsRequired ?? 0);
        if (a.SlotsRequired > 0 && used + a.SlotsRequired > capacity)
            throw CharacterChange.Refuse("attachmentInstanceId",
                L.T($"{weapon.Name} tem {capacity} Attachment Slots, {used} em uso; {item.Name} ocupa {a.SlotsRequired} ({CharacterRuleBook.Page(rules.Document.Combat.AttachmentSlots.Reference)}).", $"{weapon.Name} has {capacity} Attachment Slots, {used} in use; {item.Name} takes {a.SlotsRequired} ({CharacterRuleBook.Page(rules.Document.Combat.AttachmentSlots.Reference)})."));

        // One unit of a stack is split off, so the rest stays available.
        if (entry.Quantity > 1)
        {
            sheet = Replace(sheet, entry with { Quantity = entry.Quantity - 1 });
            change.Sheet = sheet;
            var single = entry with { InstanceId = change.NewId("it"), Quantity = 1, PricePaid = null };
            sheet = sheet with { Inventory = [.. sheet.Inventory, single] };
            entry = single;
        }
        change.Log(CharacterEventKind.Edit, "attach", L.T($"{item.Name} instalado em {weapon.Name}", $"{item.Name} installed on {weapon.Name}") +
                                                      (a.Conditions is { Length: > 0 } c ? L.T($" (condições: {c}).", $" (conditions: {c}).") : "."));
        sheet = Replace(sheet, entry with { State = ItemState.Equipped });
        return Replace(sheet, Find(sheet, weaponEntry.InstanceId) with { Weapon = state with { Attachments = [.. state.Attachments, entry.InstanceId] } });
    }

    public CharacterSheet Detach(CharacterSheet sheet, AttachRequest request, CharacterChange change)
    {
        var weaponEntry = Find(sheet, request.WeaponInstanceId);
        var state = weaponEntry.Weapon ?? new WeaponState();
        if (!state.Attachments.Contains(request.AttachmentInstanceId))
            throw CharacterChange.Refuse("attachmentInstanceId", L.T("Esse acessório não está nessa arma.", "That attachment isn't on that weapon."));
        var entry = Find(sheet, request.AttachmentInstanceId);
        change.Log(CharacterEventKind.Edit, "detach", L.T($"{change.Item(entry.ItemId).Name} retirado de {change.Item(weaponEntry.ItemId).Name}.", $"{change.Item(entry.ItemId).Name} taken off {change.Item(weaponEntry.ItemId).Name}."));
        sheet = Replace(sheet, entry with { State = ItemState.Carried });
        return Replace(sheet, Find(sheet, weaponEntry.InstanceId) with { Weapon = state with { Attachments = state.Attachments.Where(a => a != entry.InstanceId).ToList() } });
    }

    /// <summary>Undoes a purchase made during creation: refunds it and, for cyberware, the Humanity it cost.</summary>
    public CharacterSheet UndoPurchase(CharacterSheet sheet, string instanceId, CharacterChange change)
    {
        CharacterCreationService.RequireCreating(sheet);
        var entry = sheet.Inventory.FirstOrDefault(e => e.InstanceId == instanceId);
        if (entry is not null)
        {
            if (entry.Origin != ItemOrigin.Purchase) throw CharacterChange.Refuse("instanceId", L.T("Só compras podem ser desfeitas.", "Only purchases can be undone."));
            sheet = Refund(sheet, entry.PaidFrom, entry.PricePaid ?? 0, change.Item(entry.ItemId).Name, change);
            var inventory = WearableOperations.Unlink(sheet.Inventory, instanceId).Where(e => e.InstanceId != instanceId)
                .Select(e => e.Weapon is { } w && w.Attachments.Contains(instanceId) ? e with { Weapon = w with { Attachments = w.Attachments.Where(a => a != instanceId).ToList() } } : e)
                .ToList();
            return sheet with { Inventory = inventory };
        }

        var piece = sheet.Cyberware.FirstOrDefault(c => c.InstanceId == instanceId)
                    ?? throw CharacterChange.Refuse("instanceId", L.T("Item não encontrado no personagem.", "Item not found on the character."));
        if (piece.Origin != ItemOrigin.Purchase) throw CharacterChange.Refuse("instanceId", L.T("Só compras podem ser desfeitas.", "Only purchases can be undone."));
        var removed = Descendants(sheet, piece.InstanceId).Prepend(piece).ToList();
        if (removed.Any(r => r.Origin != ItemOrigin.Purchase))
            throw CharacterChange.Refuse("instanceId", L.T("Há implantes do pacote inicial instalados nessa peça.", "There are starting package implants installed in that piece."));
        foreach (var r in removed)
            sheet = Refund(sheet, r.PaidFrom, r.PricePaid ?? 0, change.Item(r.ItemId).Name, change);
        var hl = removed.Sum(r => r.HumanityLossApplied);
        change.Log(CharacterEventKind.Humanity, "undo-cyberware", L.T($"Compra de cyberware desfeita na criação: {hl} de Humanidade devolvidos.", $"Cyberware purchase undone during creation: {hl} Humanity returned."), hl);
        var ids = removed.Select(r => r.InstanceId).ToHashSet();
        return sheet with
        {
            Cyberware = sheet.Cyberware.Where(c => !ids.Contains(c.InstanceId)).ToList(),
            Humanity = sheet.Humanity with { Lost = sheet.Humanity.Lost - hl },
        };
    }

    private static CharacterSheet Refund(CharacterSheet sheet, BudgetPool pool, int amount, string what, CharacterChange change)
    {
        change.Log(CharacterEventKind.Ledger, "refund", L.T($"Compra desfeita: {what}.", $"Purchase undone: {what}."), amount);
        return pool == BudgetPool.Balance ? sheet with { Wallet = sheet.Wallet with { Balance = sheet.Wallet.Balance + amount } } : sheet;
    }

    private static IEnumerable<CharacterCyberware> Descendants(CharacterSheet sheet, string id) =>
        sheet.Cyberware.Where(c => c.ParentInstanceId == id).SelectMany(c => Descendants(sheet, c.InstanceId).Prepend(c));

    // ------------------------------------------------------------ equipment

    public CharacterSheet Equip(CharacterSheet sheet, EquipRequest request, CharacterChange change)
    {
        var entry = Find(sheet, request.InstanceId);
        var item = change.Item(entry.ItemId);
        ArmorSlot? worn = null;
        if (request.State == ItemState.Equipped && item.Armor is { Type: ArmorType.BodyArmor })
            worn = request.Worn ?? throw CharacterChange.Refuse("worn", L.T("Escolha onde vestir a armadura: cabeça ou corpo.", "Pick where to wear the armor: head or body."));
        else if (request.Worn is not null)
            throw CharacterChange.Refuse("worn", L.T($"{item.Name} não é uma armadura de corpo ou cabeça.", $"{item.Name} isn't body or head armor."));

        change.Log(CharacterEventKind.Edit, "equip",
            $"{item.Name}: {StateLabel(request.State)}{(worn is null ? "" : worn == ArmorSlot.Head ? " (cabeça)" : " (corpo)")}.");
        return Replace(sheet, entry with { State = request.State, Worn = worn });
    }

    // ------------------------------------------------------------ weapons

    /// <summary>
    /// Spends ammunition. Autofire and Suppressive Fire cost 10 bullets and need a weapon with Autofire (CP:R p.173–174).
    /// </summary>
    public CharacterSheet Fire(CharacterSheet sheet, FireRequest request, CharacterChange change)
    {
        var entry = Find(sheet, request.InstanceId);
        var item = change.Item(entry.ItemId);
        if (item.Weapon is not { } w || w.Calibers.Count == 0) throw CharacterChange.Refuse("instanceId", L.T($"{item.Name} não usa munição.", $"{item.Name} doesn't use ammo."));
        var state = entry.Weapon ?? new WeaponState();

        int rounds;
        if (request.Mode == FireMode.Single)
            rounds = request.Rounds ?? 1;
        else
        {
            if (w.AutofireMultiplier is null)
                throw CharacterChange.Refuse("mode", L.T($"{item.Name} não tem Autofire, então não faz {ModeLabel(request.Mode)}.", $"{item.Name} has no Autofire, so it can't do {ModeLabel(request.Mode)}."));
            var rule = request.Mode == FireMode.Autofire ? rules.Document.Combat.Autofire : null;
            rounds = rule?.Value.Bullets ?? rules.Document.Combat.SuppressiveFire.Value.Bullets;
        }
        if (rounds < 1) throw CharacterChange.Refuse("rounds", L.T("Informe quantas balas foram usadas.", "Enter how many rounds were used."));
        if (state.LoadedRounds < rounds)
            throw CharacterChange.Refuse("rounds", L.T($"{ModeLabel(request.Mode)} precisa de {rounds} bala(s); há {state.LoadedRounds} no pente ", $"{ModeLabel(request.Mode)} needs {rounds} round(s); there are {state.LoadedRounds} in the magazine ") +
                                                   $"({(request.Mode == FireMode.Single ? "CP:R p.170" : rules.Document.Combat.Autofire.Reference)}).");

        change.Log(CharacterEventKind.Combat, "fire", $"{item.Name}: {ModeLabel(request.Mode)} ({rounds} bala(s)).",
            -rounds, state.LoadedRounds.ToString(), (state.LoadedRounds - rounds).ToString());
        return Replace(sheet, entry with { Weapon = state with { LoadedRounds = state.LoadedRounds - rounds } });
    }

    /// <summary>
    /// Fills the magazine from a compatible ammunition entry. Switching ammunition type first returns the loaded rounds
    /// to the inventory, so no bullet is created or lost.
    /// </summary>
    public CharacterSheet Reload(CharacterSheet sheet, ReloadRequest request, CharacterChange change)
    {
        var entry = Find(sheet, request.InstanceId);
        var item = change.Item(entry.ItemId);
        if (item.Weapon is not { } w || w.Calibers.Count == 0 || w.Magazine is not { } magazine)
            throw CharacterChange.Refuse("instanceId", L.T($"{item.Name} não tem pente para recarregar.", $"{item.Name} has no magazine to reload."));

        var compatible = sheet.Inventory
            .Where(e => e.Rounds > 0 && change.Catalog.Find(e.ItemId)?.Ammo is { } a
                        && (e.Caliber is { } c ? w.Calibers.Contains(c) : a.Calibers.Any(w.Calibers.Contains)))
            .ToList();
        var ammo = request.AmmoInstanceId is { } id
            ? compatible.FirstOrDefault(e => e.InstanceId == id) ?? throw CharacterChange.Refuse("ammoInstanceId", L.T($"Essa munição não serve em {item.Name}.", $"That ammo doesn't fit {item.Name}."))
            : compatible.OrderByDescending(e => e.ItemId == entry.Weapon?.LoadedAmmoItemId).ThenByDescending(e => e.Rounds).FirstOrDefault()
              ?? throw CharacterChange.Refuse("ammoInstanceId", L.T($"Sem munição compatível com {item.Name} no inventário.", $"No ammo compatible with {item.Name} in the inventory."));

        var state = entry.Weapon ?? new WeaponState();
        if (state.LoadedRounds > 0 && state.LoadedAmmoItemId is { } loaded && loaded != ammo.ItemId)
        {
            sheet = ReturnRounds(sheet, loaded, ammo.Caliber, state.LoadedRounds, change);
            state = state with { LoadedRounds = 0 };
            ammo = sheet.Inventory.First(e => e.InstanceId == ammo.InstanceId);
        }
        var take = Math.Min(magazine - state.LoadedRounds, ammo.Rounds ?? 0);
        if (take <= 0) throw CharacterChange.Refuse("instanceId", L.T($"{item.Name} já está com o pente cheio.", $"{item.Name} already has a full magazine."));

        sheet = Replace(sheet, ammo with { Rounds = ammo.Rounds - take });
        change.Log(CharacterEventKind.Combat, "reload", L.T($"{item.Name} recarregada com {take} bala(s) de {change.Item(ammo.ItemId).Name}.", $"{item.Name} reloaded with {take} round(s) of {change.Item(ammo.ItemId).Name}."),
            take, state.LoadedRounds.ToString(), (state.LoadedRounds + take).ToString());
        return Replace(sheet, Find(sheet, entry.InstanceId) with
        {
            Weapon = state with { LoadedRounds = state.LoadedRounds + take, LoadedAmmoItemId = ammo.ItemId },
        });
    }

    private static CharacterSheet ReturnRounds(CharacterSheet sheet, int ammoItemId, Caliber? caliber, int rounds, CharacterChange change)
    {
        var target = sheet.Inventory.FirstOrDefault(e => e.ItemId == ammoItemId && e.Caliber == caliber);
        if (target is not null) return Replace(sheet, target with { Rounds = (target.Rounds ?? 0) + rounds });
        change.Sheet = sheet;
        return sheet with
        {
            Inventory =
            [
                .. sheet.Inventory,
                new InventoryEntry { InstanceId = change.NewId("it"), ItemId = ammoItemId, Caliber = caliber, Rounds = rounds, Origin = ItemOrigin.Manual, PricePaid = 0 },
            ],
        };
    }

    // ------------------------------------------------------------ money and luck

    public static CharacterSheet AdjustBalance(CharacterSheet sheet, AdjustBalanceRequest request, CharacterChange change)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw CharacterChange.Refuse("reason", L.T("Explique o motivo do ajuste.", "Explain the reason for the adjustment."));
        var after = sheet.Wallet.Balance + request.Amount;
        if (after < 0) throw CharacterChange.Refuse("amount", L.T("O saldo não pode ficar negativo.", "The balance can't go negative."));
        change.Log(CharacterEventKind.Ledger, "manual-adjustment", request.Reason.Trim(), request.Amount,
            sheet.Wallet.Balance.ToString(), after.ToString(), manual: true);
        return sheet with { Wallet = sheet.Wallet with { Balance = after } };
    }

    public static CharacterSheet SetLuckSpent(CharacterSheet sheet, int spent, CharacterChange change)
    {
        var luck = sheet.Stat("LUCK");
        if (spent < 0 || spent > luck) throw CharacterChange.Refuse("spent", L.T($"Use entre 0 e {luck} pontos de LUCK.", $"Use between 0 and {luck} LUCK points."));
        change.Log(CharacterEventKind.Combat, "luck", L.T($"LUCK gasta na sessão: {spent} de {luck}.", $"LUCK spent this session: {spent} of {luck}."), spent - sheet.Combat.LuckSpent,
            sheet.Combat.LuckSpent.ToString(), spent.ToString(), manual: true);
        return sheet with { Combat = sheet.Combat with { LuckSpent = spent } };
    }

    // ------------------------------------------------------------ helpers

    private static InventoryEntry Find(CharacterSheet sheet, string instanceId) =>
        sheet.Inventory.FirstOrDefault(e => e.InstanceId == instanceId)
        ?? throw CharacterChange.Refuse("instanceId", L.T("Item não encontrado no inventário.", "Item not found in the inventory."));

    private static CharacterSheet Replace(CharacterSheet sheet, InventoryEntry entry) =>
        sheet with { Inventory = sheet.Inventory.Select(e => e.InstanceId == entry.InstanceId ? entry : e).ToList() };

    private static string StateLabel(ItemState state) => state switch
    {
        ItemState.Equipped => "equipado",
        ItemState.Stored => "guardado",
        _ => "carregado",
    };

    private static string ModeLabel(FireMode mode) => mode switch
    {
        FireMode.Autofire => "Autofire",
        FireMode.Suppressive => "Suppressive Fire",
        _ => "Disparo",
    };
}
