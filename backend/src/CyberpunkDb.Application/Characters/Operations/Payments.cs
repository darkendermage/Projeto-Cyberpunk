using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>Charging purchases to the balance or, during a Complete Package creation, to the creation budgets.</summary>
public static class Payments
{
    /// <summary>
    /// The budget a purchase uses: after creation always the balance; during a Complete Package the fashion budget for
    /// Fashion/Fashionware and the general one otherwise, unless the player picked one (CP:R p.104–105).
    /// </summary>
    public static BudgetPool PoolFor(CharacterRuleBook rules, CharacterSheet sheet, bool fashion, BudgetPool? requested)
    {
        var completePackage = !sheet.Creation.Finalized && sheet.Creation.Method == CreationMethod.CompletePackage;
        if (!completePackage)
        {
            if (requested is BudgetPool.CreationGear or BudgetPool.CreationFashion)
                throw CharacterChange.Refuse("pool", "Os orçamentos de criação só existem durante a criação de um Complete Package.");
            return BudgetPool.Balance;
        }
        var pool = requested is null or BudgetPool.Balance ? (fashion ? BudgetPool.CreationFashion : BudgetPool.CreationGear) : requested.Value;
        if (pool == BudgetPool.CreationFashion && !fashion)
            throw CharacterChange.Refuse("pool", "Esse item não é Fashion nem Fashionware: pague com o orçamento geral " +
                                                  $"({CharacterRuleBook.Page(rules.Document.Creation.Methods.CompletePackage.FashionBudget.Reference)}).");
        return pool;
    }

    /// <summary>What is left in a budget before this purchase.</summary>
    public static int Available(CharacterRuleBook rules, CharacterSheet sheet, BudgetPool pool)
    {
        if (pool == BudgetPool.Balance) return sheet.Wallet.Balance;
        var limits = CreationLimits.For(rules, sheet.Creation);
        var limit = pool == BudgetPool.CreationFashion ? limits.FashionBudget! : limits.GearBudget!;
        var spent = sheet.Inventory.Where(e => e.PaidFrom == pool).Sum(e => e.PricePaid ?? 0)
                    + sheet.Cyberware.Where(e => e.PaidFrom == pool).Sum(e => e.PricePaid ?? 0);
        return limit.Value - spent;
    }

    /// <summary>Charges a cost or refuses the whole operation; nothing is changed when the money isn't there.</summary>
    public static CharacterSheet Charge(CharacterRuleBook rules, CharacterSheet sheet, BudgetPool pool, int cost, string what, CharacterChange change)
    {
        var available = Available(rules, sheet, pool);
        if (cost > available)
        {
            var label = pool switch
            {
                BudgetPool.CreationFashion => "do orçamento de moda",
                BudgetPool.CreationGear => "do orçamento geral",
                _ => "de saldo",
            };
            throw CharacterChange.Refuse("itemId", $"Saldo insuficiente: {what} custa {CharacterValidator.Eb(cost)} e restam {CharacterValidator.Eb(available)} {label}.");
        }
        if (cost == 0) return sheet;

        if (pool == BudgetPool.Balance)
        {
            change.Log(CharacterEventKind.Ledger, "purchase", $"Compra: {what}.", -cost, available.ToString(), (available - cost).ToString());
            return sheet with { Wallet = sheet.Wallet with { Balance = available - cost } };
        }
        var budget = pool == BudgetPool.CreationFashion ? "orçamento de moda" : "orçamento geral";
        change.Log(CharacterEventKind.Ledger, "purchase", $"Compra na criação ({budget}): {what}.", -cost, available.ToString(), (available - cost).ToString());
        return sheet;
    }
}
