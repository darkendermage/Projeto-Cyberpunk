using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>Recurring expenses and leaving items behind. Charges happen only when the player records them.</summary>
public sealed class EconomyOperations(CharacterCalculator calculator)
{
    /// <summary>
    /// Pays housing and lifestyle for one or more months from the balance (CP:R p.377): the paid-until date moves
    /// forward from where it was, so paying an overdue month doesn't skip the debt.
    /// </summary>
    public CharacterSheet PayMonths(CharacterSheet sheet, PayMonthsRequest request, CharacterChange change)
    {
        if (!sheet.Creation.Finalized) throw CharacterChange.Refuse("creation", "Conclua a criação antes de registrar pagamentos.");
        if (request.Months is < 1 or > 12) throw CharacterChange.Refuse("months", "Pague de 1 a 12 meses por vez.");
        var today = DateOnly.FromDateTime(change.Now);
        var housing = calculator.ComputeHousing(sheet, today);
        if (housing.MonthlyTotal <= 0) throw CharacterChange.Refuse("months", "Não há aluguel nem lifestyle com custo para pagar.");

        var cost = housing.MonthlyTotal * request.Months;
        var balance = sheet.Wallet.Balance;
        if (cost > balance)
            throw CharacterChange.Refuse("months", $"O mês custa {CharacterValidator.Eb(cost)} e o saldo é {CharacterValidator.Eb(balance)}.");

        var from = sheet.Housing.PaidUntil ?? today;
        var until = from.AddMonths(request.Months);
        change.Log(CharacterEventKind.Ledger, "monthly-payment",
            $"{request.Months} mês(es) de {housing.Housing ?? "moradia"} ({CharacterValidator.Eb(housing.Rent)}) e lifestyle {housing.Lifestyle ?? "—"} " +
            $"({CharacterValidator.Eb(housing.LifestyleCost)}); pago até {until:dd/MM/yyyy}.",
            -cost, balance.ToString(), (balance - cost).ToString());
        return sheet with
        {
            Wallet = sheet.Wallet with { Balance = balance - cost },
            Housing = sheet.Housing with { PaidUntil = until },
        };
    }

    /// <summary>
    /// Removes items after creation: discarded, lost or sold. The Core sets no resale price ("everything is worth what
    /// its purchaser will pay for it", CP:R p.385), so the amount is decided at the table and logged as manual.
    /// </summary>
    public static CharacterSheet RemoveItem(CharacterSheet sheet, RemoveItemRequest request, CharacterChange change)
    {
        if (!sheet.Creation.Finalized)
            throw CharacterChange.Refuse("creation", "Durante a criação, desfaça a compra em vez de remover o item.");
        var entry = sheet.Inventory.FirstOrDefault(e => e.InstanceId == request.InstanceId)
                    ?? throw CharacterChange.Refuse("instanceId", "Item não encontrado no inventário.");
        var item = change.Item(entry.ItemId);
        if (request.SaleAmount is < 0) throw CharacterChange.Refuse("saleAmount", "O valor da venda não pode ser negativo.");
        if (request.SaleAmount > 0 && string.IsNullOrWhiteSpace(request.Reason))
            throw CharacterChange.Refuse("reason", "Diga para quem e por que foi vendido.");

        var held = entry.Rounds ?? entry.Quantity;
        var amount = request.Quantity ?? held;
        if (amount < 1 || amount > held) throw CharacterChange.Refuse("quantity", $"Quantidade entre 1 e {held}.");

        var inventory = sheet.Inventory.ToList();
        var remaining = held - amount;
        if (remaining == 0)
        {
            inventory.RemoveAll(e => e.InstanceId == entry.InstanceId);
            inventory = inventory.Select(e => e.Weapon is { } w && w.Attachments.Contains(entry.InstanceId)
                ? e with { Weapon = w with { Attachments = w.Attachments.Where(a => a != entry.InstanceId).ToList() } }
                : e).ToList();
        }
        else
        {
            var i = inventory.FindIndex(e => e.InstanceId == entry.InstanceId);
            inventory[i] = entry.Rounds is not null ? entry with { Rounds = remaining } : entry with { Quantity = remaining };
        }

        var what = $"{item.Name}{(amount > 1 ? $" x{amount}" : "")}";
        var reason = request.Reason?.Trim();
        var balance = sheet.Wallet.Balance;
        if (request.SaleAmount is > 0 and var sale)
        {
            change.Log(CharacterEventKind.Ledger, "sale", $"Venda de {what}: {reason}", sale, balance.ToString(), (balance + sale).ToString(), manual: true);
            balance += sale;
        }
        else
            change.Log(CharacterEventKind.Edit, "discard", $"{what} removido do inventário{(reason is { Length: > 0 } ? $": {reason}" : ".")}", manual: true);

        return sheet with
        {
            Inventory = inventory,
            Wallet = sheet.Wallet with { Balance = balance },
            Style = sheet.Style with { OutfitItems = sheet.Style.OutfitItems.Where(id => remaining > 0 || id != entry.InstanceId).ToList() },
        };
    }
}
