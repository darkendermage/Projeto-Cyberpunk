using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets.Trading;

/// <summary>What a purchase costs after the Haggle deal.</summary>
public sealed record DealOutcome(int Total, int Delivered, int PaidNow, int DueLater, string? DealId, string Description);

/// <summary>
/// Prices of a market: the condition of an item (IR5 p.5), the Haggle opposed check and its deals (CP:R p.129–130,
/// p.159–161). No state: everything comes from the rule book and the listing.
/// </summary>
public sealed class MarketPricing(NightMarketRuleBook rules)
{
    private ConditionRules Condition => rules.Document.Condition;

    /// <summary>What the item is worth in its condition: Damaged one category lower, Destroyed two, Beyond Repair nothing.</summary>
    public (int Price, PriceCategory Category) Value(int basePrice, PriceCategory baseCategory, ItemCondition condition) => condition switch
    {
        ItemCondition.Damaged => Down(baseCategory, Condition.DamagedSteps.Value),
        ItemCondition.Destroyed => Down(baseCategory, Condition.DestroyedSteps.Value),
        ItemCondition.BeyondRepair => (Condition.BeyondRepair.Value, rules.Document.Condition.Floor.Value),
        _ => (basePrice, baseCategory),
    };

    public (int Price, PriceCategory Category) Value(MarketListing l) => Value(l.BasePrice, l.BaseCategory, l.Condition);

    /// <summary>The asking price: a hidden condition (a scam) is sold at the full price.</summary>
    public (int Price, PriceCategory Category) Asked(MarketListing l) =>
        l.ConditionHidden ? (l.BasePrice, l.BaseCategory) : Value(l);

    public IReadOnlyList<string> Warnings(MarketListing l, MarketOrganizer organizer)
    {
        var warnings = new List<string>();
        if (l.Condition == ItemCondition.Damaged && l.BasePrice <= Condition.LowValueDamaged.Value)
            warnings.Add($"Poucos Fixers compram um item Damaged de {Condition.LowValueDamaged.Value}eb ou menos ({Condition.LowValueDamaged.Source}).");
        if (l.Condition == ItemCondition.BeyondRepair)
            warnings.Add($"Destroyed Beyond Repair: quase ninguém compra ({Condition.BeyondRepair.Source}).");
        if (l.ConditionHidden && l.Condition != ItemCondition.Normal)
            warnings.Add($"Golpe: vendido como novo. Um Fixer com Operator {Condition.DetectRank.Value} ou mais percebe o dano, se não foi disfarçado ({Condition.DetectRank.Source}).");
        if (rules.Reach(organizer.OperatorRank) is { } reach && l.BaseCategory > reach && organizer.OperatorRank < rules.Document.Operator.NightMarketRank.Value)
            warnings.Add($"Acima do Reach do organizador ({reach}); fora de um Night Market ele não encontra este item ({rules.Document.Operator.Reach.Source}).");
        return warnings;
    }

    private (int, PriceCategory) Down(PriceCategory category, int steps)
    {
        var lower = rules.Lower(category, steps);
        return (rules.PriceOf(lower), lower);
    }

    // ------------------------------------------------------------------ Haggle

    /// <summary>
    /// COOL + Trading + Operator Rank + 1d10 on each side; the buyer must beat the seller (ties go to the Defender).
    /// A natural 10 adds another d10, a natural 1 subtracts one (CP:R p.130).
    /// </summary>
    public HaggleResult Haggle(HaggleRequest request, IDiceRoller dice, PriceCategory? itemCategory = null, int quantity = 1)
    {
        var (buyerRolls, buyerDice) = Roll(request.BuyerRolls, dice, "buyerRolls");
        var (sellerRolls, sellerDice) = Roll(request.SellerRolls, dice, "sellerRolls");
        var buyer = request.Buyer.Trading + request.Buyer.Rank + buyerDice;
        var seller = request.Seller.Trading + request.Seller.Rank + sellerDice;
        var success = request.Buyer.Rank > 0 && buyer > seller;

        string summary;
        if (request.Buyer.Rank <= 0)
            summary = $"Só um Fixer pechincha: sem Operator Rank não há acordo ({rules.Document.Operator.HaggleFormula.Source}).";
        else if (success)
            summary = $"{buyer} contra {seller}: o comprador venceu e pode fazer 1 acordo de Rank {request.Buyer.Rank} ou abaixo.";
        else
            summary = buyer == seller
                ? $"{buyer} contra {seller}: empate fica com o vendedor ({rules.Document.Operator.HaggleTie.Source})."
                : $"{buyer} contra {seller}: o vendedor venceu, sem acordo.";

        var deals = rules.Document.Operator.Deals.Value
            .Where(d => d.MinRank <= Math.Max(request.Buyer.Rank, 0))
            .Select(d => Option(d, success, itemCategory, quantity))
            .ToList();
        return new HaggleResult(buyer, seller, buyerRolls, sellerRolls, success, summary, deals);
    }

    private static DealOption Option(HaggleDeal d, bool success, PriceCategory? category, int quantity)
    {
        string? reason = d.Kind switch
        {
            DealKind.JobPay => "Vale para o pagamento de um Job, não para compras.",
            DealKind.ExtraItem when quantity < (d.MinQuantity ?? 0) => $"Só comprando {d.MinQuantity} ou mais do mesmo item.",
            DealKind.SplitPayment when category is not { } c || d.Categories is { } cats && !cats.Contains(c) => "Só para itens Luxury ou Super Luxury.",
            _ => null,
        };
        if (!success) reason ??= "O Haggle não deu certo.";
        return new DealOption(d.Id, d.Label, d.Text, d.MinRank, reason is null, reason);
    }

    private static (int[] Rolls, int Total) Roll(int[]? typed, IDiceRoller dice, string field)
    {
        if (typed is { Length: > 2 } || typed?.Any(r => r is < 1 or > 10) == true)
            throw RequestValidationException.For(field, "Informe o d10 (1 a 10) e, se ele der 10 ou 1, o d10 extra.");
        var first = typed is { Length: > 0 } ? typed[0] : dice.Roll(10);
        if (first is not (10 or 1)) return ([first], first);
        var second = typed is { Length: > 1 } ? typed[1] : dice.Roll(10);
        return ([first, second], first == 10 ? first + second : first - second);
    }

    /// <summary>Applies the chosen deal to a purchase of <paramref name="quantity"/> units at <paramref name="unitPrice"/>.</summary>
    public DealOutcome Apply(HaggleResult? haggle, string? dealId, int unitPrice, PriceCategory category, int quantity)
    {
        var total = unitPrice * quantity;
        if (dealId is null) return new DealOutcome(total, quantity, total, 0, null, "Preço de mercado.");
        var option = haggle?.Deals.FirstOrDefault(d => string.Equals(d.Id, dealId, StringComparison.OrdinalIgnoreCase));
        var deal = rules.Deal(dealId);
        if (haggle is null || option is null || deal is null)
            throw RequestValidationException.For("dealId", "Acordo indisponível: é preciso vencer o Haggle com Operator Rank igual ou acima do acordo.");
        if (!option.Applies) throw RequestValidationException.For("dealId", option.Reason ?? "Acordo indisponível.");

        return deal.Kind switch
        {
            DealKind.Discount => new DealOutcome(total - Percent(total, deal.Percent ?? 0), quantity, total - Percent(total, deal.Percent ?? 0), 0, deal.Id,
                $"{deal.Percent}% a menos pelo Haggle."),
            DealKind.ExtraItem => new DealOutcome(total, quantity + 1, total, 0, deal.Id, "Levou 5 ou mais e ganhou mais um."),
            DealKind.SplitPayment => new DealOutcome(total, quantity, total - total / 2, total / 2, deal.Id, "Metade agora, metade em um mês."),
            _ => throw RequestValidationException.For("dealId", "Este acordo não vale para compras."),
        };
    }

    /// <summary>Percent of an amount, rounded down to whole eb (complementary).</summary>
    public static int Percent(int amount, int percent) => amount * percent / 100;

    // ------------------------------------------------------------------ selling to a Fixer

    public SellQuote SellQuote(string? name, int basePrice, PriceCategory baseCategory, SellQuoteRequest request, IDiceRoller dice)
    {
        var notes = new List<string>();
        var damaged = request.Condition != ItemCondition.Normal;
        var notices = damaged && request.Disclosure == ConditionDisclosure.Silent && request.FixerRank >= Condition.DetectRank.Value;
        // A damage the Fixer doesn't know about (kept quiet or disguised) is paid as if the item were whole.
        var known = !damaged || request.Disclosure == ConditionDisclosure.Disclosed || notices;
        var (offer, offerCategory) = known ? Value(basePrice, baseCategory, request.Condition) : (basePrice, baseCategory);

        if (damaged)
            notes.Add(known
                ? $"{request.Condition}: vale {offer}eb ({offerCategory}) para quem sabe do dano ({Condition.DamagedSteps.Source})."
                : request.Disclosure == ConditionDisclosure.Disguised
                    ? $"Dano disfarçado: o Fixer paga como se o item estivesse inteiro, a não ser que o Mestre peça um teste contra o Forgery ({Condition.DetectRank.Source})."
                    : $"O Fixer não percebeu o dano: paga como se o item estivesse inteiro ({Condition.DetectRank.Source}).");
        if (notices) notes.Add($"Com Operator {request.FixerRank}, o Fixer percebe o dano na hora ({Condition.DetectRank.Source}).");
        if (request.Condition == ItemCondition.Damaged && basePrice <= Condition.LowValueDamaged.Value && known)
            notes.Add($"Poucos Fixers compram um item Damaged de {Condition.LowValueDamaged.Value}eb ou menos ({Condition.LowValueDamaged.Source}).");
        if (request.Condition == ItemCondition.BeyondRepair)
            notes.Add($"Poucos Fixers compram itens Destroyed Beyond Repair ({Condition.BeyondRepair.Source}).");
        notes.Add($"{rules.Document.Condition.SellingIsGmCall.Value} ({rules.Document.Condition.SellingIsGmCall.Source})");

        HaggleResult? haggle = null;
        if (request.Haggle is { } h)
        {
            haggle = Haggle(h, dice, offerCategory);
            if (request.DealId is { } id)
            {
                var deal = rules.Deal(id) ?? throw RequestValidationException.For("dealId", "Acordo inexistente.");
                var option = haggle.Deals.FirstOrDefault(d => d.Id == deal.Id);
                if (deal.Kind != DealKind.Discount)
                    throw RequestValidationException.For("dealId", "Vendendo, só valem os acordos de 10% e 20% a mais.");
                if (option is not { Applies: true })
                    throw RequestValidationException.For("dealId", option?.Reason ?? "Acordo de Rank acima do seu.");
                offer += Percent(offer, deal.Percent ?? 0);
                notes.Add($"Haggle: {deal.Percent}% a mais vendendo ({rules.Document.Operator.Deals.Source}).");
            }
        }
        return new SellQuote(name, basePrice, baseCategory, offer, offerCategory, notices, notes, haggle);
    }
}
