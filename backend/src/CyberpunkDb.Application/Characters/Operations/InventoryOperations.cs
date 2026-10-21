using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

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
                throw CharacterChange.Refuse("received", "Sem custo, o item vem de troca, presente ou saque.");
            if (!sheet.Creation.Finalized)
                throw CharacterChange.Refuse("received", "Itens sem custo só entram depois da criação: na criação o equipamento sai do orçamento.");
        }
        else if (item.Price is null) throw CharacterChange.Refuse("itemId", $"{item.Name} não tem preço de catálogo.");
        if (request.Quantity < 1 || request.Quantity > 999) throw CharacterChange.Refuse("quantity", "Quantidade inválida.");

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
                throw CharacterChange.Refuse("caliber", $"Escolha o calibre de {item.Name}.");
            rounds = ammo.Kind == AmmoKind.Round ? (ammo.PerPack ?? 1) * request.Quantity : null;
        }

        ArmorSlot? worn = null;
        var equip = request.Equip && (item.Weapon is not null || item.Armor is not null);
        if (equip && item.Armor is { Type: ArmorType.BodyArmor })
            worn = request.Worn ?? throw CharacterChange.Refuse("worn", "Escolha onde vestir a armadura: cabeça ou corpo.");

        var what = request.Quantity > 1 ? $"{item.Name} x{request.Quantity}" : item.Name;
        var origin = received ?? ItemOrigin.Purchase;
        var cost = received is null ? item.Price!.Value * request.Quantity : 0;
        if (received is null) sheet = Payments.Charge(rules, sheet, pool, cost, what, change);
        else
            change.Log(CharacterEventKind.Ledger, "receive", $"{OriginLabel(origin)}: {what}, sem custo" +
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
        ItemOrigin.Trade => "Recebido em troca",
        ItemOrigin.Gift => "Recebido de presente",
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
        if (weapon.Weapon is not { } w) throw CharacterChange.Refuse("weaponInstanceId", $"{weapon.Name} não é uma arma.");
        var entry = Find(sheet, request.AttachmentInstanceId);
        var item = change.Item(entry.ItemId);
        if (item.Attachment is not { } a) throw CharacterChange.Refuse("attachmentInstanceId", $"{item.Name} não é um acessório de arma.");
        if (entry.Quantity <= 1 && sheet.Inventory.Any(e => e.Weapon?.Attachments.Contains(entry.InstanceId) == true))
            throw CharacterChange.Refuse("attachmentInstanceId", $"{item.Name} já está instalado em outra arma.");

        if (a.Classes.Count > 0 && !a.Classes.Contains(w.Class))
            throw CharacterChange.Refuse("attachmentInstanceId", $"{item.Name} não serve em {weapon.Name}: só em {string.Join(", ", a.Classes)}.");
        if (w.Exotic && !a.ExoticAllowed)
            throw CharacterChange.Refuse("attachmentInstanceId", $"{weapon.Name} é exótica e {item.Name} não pode ser instalado nela.");

        var state = weaponEntry.Weapon ?? new WeaponState();
        var installed = state.Attachments.Select(id => sheet.Inventory.FirstOrDefault(e => e.InstanceId == id))
            .Where(e => e is not null).Select(e => change.Item(e!.ItemId)).ToList();
        if (installed.Any(i => i.Id == item.Id))
            throw CharacterChange.Refuse("attachmentInstanceId", $"{weapon.Name} já tem {item.Name}.");
        var capacity = w.Calibers.Count > 0 && !w.Exotic ? rules.Document.Combat.AttachmentSlots.Value : 0;
        var used = installed.Sum(i => i.Attachment?.SlotsRequired ?? 0);
        if (a.SlotsRequired > 0 && used + a.SlotsRequired > capacity)
            throw CharacterChange.Refuse("attachmentInstanceId",
                $"{weapon.Name} tem {capacity} Attachment Slots, {used} em uso; {item.Name} ocupa {a.SlotsRequired} ({CharacterRuleBook.Page(rules.Document.Combat.AttachmentSlots.Reference)}).");

        // One unit of a stack is split off, so the rest stays available.
        if (entry.Quantity > 1)
        {
            sheet = Replace(sheet, entry with { Quantity = entry.Quantity - 1 });
            change.Sheet = sheet;
            var single = entry with { InstanceId = change.NewId("it"), Quantity = 1, PricePaid = null };
            sheet = sheet with { Inventory = [.. sheet.Inventory, single] };
            entry = single;
        }
        change.Log(CharacterEventKind.Edit, "attach", $"{item.Name} instalado em {weapon.Name}" +
                                                      (a.Conditions is { Length: > 0 } c ? $" (condições: {c})." : "."));
        sheet = Replace(sheet, entry with { State = ItemState.Equipped });
        return Replace(sheet, Find(sheet, weaponEntry.InstanceId) with { Weapon = state with { Attachments = [.. state.Attachments, entry.InstanceId] } });
    }

    public CharacterSheet Detach(CharacterSheet sheet, AttachRequest request, CharacterChange change)
    {
        var weaponEntry = Find(sheet, request.WeaponInstanceId);
        var state = weaponEntry.Weapon ?? new WeaponState();
        if (!state.Attachments.Contains(request.AttachmentInstanceId))
            throw CharacterChange.Refuse("attachmentInstanceId", "Esse acessório não está nessa arma.");
        var entry = Find(sheet, request.AttachmentInstanceId);
        change.Log(CharacterEventKind.Edit, "detach", $"{change.Item(entry.ItemId).Name} retirado de {change.Item(weaponEntry.ItemId).Name}.");
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
            if (entry.Origin != ItemOrigin.Purchase) throw CharacterChange.Refuse("instanceId", "Só compras podem ser desfeitas.");
            sheet = Refund(sheet, entry.PaidFrom, entry.PricePaid ?? 0, change.Item(entry.ItemId).Name, change);
            var inventory = sheet.Inventory.Where(e => e.InstanceId != instanceId)
                .Select(e => e.Weapon is { } w && w.Attachments.Contains(instanceId) ? e with { Weapon = w with { Attachments = w.Attachments.Where(a => a != instanceId).ToList() } } : e)
                .ToList();
            return sheet with { Inventory = inventory };
        }

        var piece = sheet.Cyberware.FirstOrDefault(c => c.InstanceId == instanceId)
                    ?? throw CharacterChange.Refuse("instanceId", "Item não encontrado no personagem.");
        if (piece.Origin != ItemOrigin.Purchase) throw CharacterChange.Refuse("instanceId", "Só compras podem ser desfeitas.");
        var removed = Descendants(sheet, piece.InstanceId).Prepend(piece).ToList();
        if (removed.Any(r => r.Origin != ItemOrigin.Purchase))
            throw CharacterChange.Refuse("instanceId", "Há implantes do pacote inicial instalados nessa peça.");
        foreach (var r in removed)
            sheet = Refund(sheet, r.PaidFrom, r.PricePaid ?? 0, change.Item(r.ItemId).Name, change);
        var hl = removed.Sum(r => r.HumanityLossApplied);
        change.Log(CharacterEventKind.Humanity, "undo-cyberware", $"Compra de cyberware desfeita na criação: {hl} de Humanidade devolvidos.", hl);
        var ids = removed.Select(r => r.InstanceId).ToHashSet();
        return sheet with
        {
            Cyberware = sheet.Cyberware.Where(c => !ids.Contains(c.InstanceId)).ToList(),
            Humanity = sheet.Humanity with { Lost = sheet.Humanity.Lost - hl },
        };
    }

    private static CharacterSheet Refund(CharacterSheet sheet, BudgetPool pool, int amount, string what, CharacterChange change)
    {
        change.Log(CharacterEventKind.Ledger, "refund", $"Compra desfeita: {what}.", amount);
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
            worn = request.Worn ?? throw CharacterChange.Refuse("worn", "Escolha onde vestir a armadura: cabeça ou corpo.");
        else if (request.Worn is not null)
            throw CharacterChange.Refuse("worn", $"{item.Name} não é uma armadura de corpo ou cabeça.");

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
        if (item.Weapon is not { } w || w.Calibers.Count == 0) throw CharacterChange.Refuse("instanceId", $"{item.Name} não usa munição.");
        var state = entry.Weapon ?? new WeaponState();

        int rounds;
        if (request.Mode == FireMode.Single)
            rounds = request.Rounds ?? 1;
        else
        {
            if (w.AutofireMultiplier is null)
                throw CharacterChange.Refuse("mode", $"{item.Name} não tem Autofire, então não faz {ModeLabel(request.Mode)}.");
            var rule = request.Mode == FireMode.Autofire ? rules.Document.Combat.Autofire : null;
            rounds = rule?.Value.Bullets ?? rules.Document.Combat.SuppressiveFire.Value.Bullets;
        }
        if (rounds < 1) throw CharacterChange.Refuse("rounds", "Informe quantas balas foram usadas.");
        if (state.LoadedRounds < rounds)
            throw CharacterChange.Refuse("rounds", $"{ModeLabel(request.Mode)} precisa de {rounds} bala(s); há {state.LoadedRounds} no pente " +
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
            throw CharacterChange.Refuse("instanceId", $"{item.Name} não tem pente para recarregar.");

        var compatible = sheet.Inventory
            .Where(e => e.Rounds > 0 && change.Catalog.Find(e.ItemId)?.Ammo is { } a
                        && (e.Caliber is { } c ? w.Calibers.Contains(c) : a.Calibers.Any(w.Calibers.Contains)))
            .ToList();
        var ammo = request.AmmoInstanceId is { } id
            ? compatible.FirstOrDefault(e => e.InstanceId == id) ?? throw CharacterChange.Refuse("ammoInstanceId", $"Essa munição não serve em {item.Name}.")
            : compatible.OrderByDescending(e => e.ItemId == entry.Weapon?.LoadedAmmoItemId).ThenByDescending(e => e.Rounds).FirstOrDefault()
              ?? throw CharacterChange.Refuse("ammoInstanceId", $"Sem munição compatível com {item.Name} no inventário.");

        var state = entry.Weapon ?? new WeaponState();
        if (state.LoadedRounds > 0 && state.LoadedAmmoItemId is { } loaded && loaded != ammo.ItemId)
        {
            sheet = ReturnRounds(sheet, loaded, ammo.Caliber, state.LoadedRounds, change);
            state = state with { LoadedRounds = 0 };
            ammo = sheet.Inventory.First(e => e.InstanceId == ammo.InstanceId);
        }
        var take = Math.Min(magazine - state.LoadedRounds, ammo.Rounds ?? 0);
        if (take <= 0) throw CharacterChange.Refuse("instanceId", $"{item.Name} já está com o pente cheio.");

        sheet = Replace(sheet, ammo with { Rounds = ammo.Rounds - take });
        change.Log(CharacterEventKind.Combat, "reload", $"{item.Name} recarregada com {take} bala(s) de {change.Item(ammo.ItemId).Name}.",
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
        if (string.IsNullOrWhiteSpace(request.Reason)) throw CharacterChange.Refuse("reason", "Explique o motivo do ajuste.");
        var after = sheet.Wallet.Balance + request.Amount;
        if (after < 0) throw CharacterChange.Refuse("amount", "O saldo não pode ficar negativo.");
        change.Log(CharacterEventKind.Ledger, "manual-adjustment", request.Reason.Trim(), request.Amount,
            sheet.Wallet.Balance.ToString(), after.ToString(), manual: true);
        return sheet with { Wallet = sheet.Wallet with { Balance = after } };
    }

    public static CharacterSheet SetLuckSpent(CharacterSheet sheet, int spent, CharacterChange change)
    {
        var luck = sheet.Stat("LUCK");
        if (spent < 0 || spent > luck) throw CharacterChange.Refuse("spent", $"Use entre 0 e {luck} pontos de LUCK.");
        change.Log(CharacterEventKind.Combat, "luck", $"LUCK gasta na sessão: {spent} de {luck}.", spent - sheet.Combat.LuckSpent,
            sheet.Combat.LuckSpent.ToString(), spent.ToString(), manual: true);
        return sheet with { Combat = sheet.Combat with { LuckSpent = spent } };
    }

    // ------------------------------------------------------------ helpers

    private static InventoryEntry Find(CharacterSheet sheet, string instanceId) =>
        sheet.Inventory.FirstOrDefault(e => e.InstanceId == instanceId)
        ?? throw CharacterChange.Refuse("instanceId", "Item não encontrado no inventário.");

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
