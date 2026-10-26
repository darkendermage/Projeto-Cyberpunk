using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets.Generation;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NightMarkets.Trading;

/// <summary>A market being changed by one command: its state, the catalog, the clock and what to tell the user.</summary>
public sealed class MarketChange(MarketState state, MarketCatalog catalog, DateTime now)
{
    public MarketState State { get; set; } = state;
    public MarketCatalog Catalog { get; } = catalog;
    public DateTime Now { get; } = now;
    public List<string> Notices { get; } = [];

    internal ListingBuilder Builder(NightMarketRuleBook rules) =>
        new(rules, Catalog, State.Letter, State.Stalls, State.Listings, State.OpenSlots, State.NextId);

    internal void Commit(ListingBuilder build) => State = State with { NextId = build.NextId };

    public void Log(string kind, string title, string? detail = null) => State.Events.Add(new MarketEvent(Now, kind, title, detail));
}

/// <summary>
/// The GM and player commands of a saved market. Purchases are standalone: they go to the market's own ledger and
/// never touch a character sheet.
/// </summary>
public sealed class MarketOperations(NightMarketRuleBook rules, MarketPricing pricing, IDiceRoller dice)
{
    // ------------------------------------------------------------------ purchases

    public void Purchase(MarketChange change, PurchaseRequest request)
    {
        var state = change.State;
        var listing = Listing(state, request.ListingId);
        if (!listing.ForSale)
            throw RequestValidationException.For("listingId", listing.Origin == ListingOrigin.Preset && state.PresetId == "toggle"
                ? L.T("No Toggle's Temple as armas são emprestadas para usar ali dentro, não vendidas (IR5 p.37).", "At Toggle's Temple weapons are lent for use inside, not sold (IR5 p.37).")
                : L.T("Este item não está à venda.", "This item isn't for sale."));
        if (request.Quantity < 1) throw RequestValidationException.For("quantity", L.T("Compre ao menos 1 unidade.", "Buy at least 1 unit."));
        if (listing.Stock is { } stock && request.Quantity > stock)
            throw RequestValidationException.For("quantity", L.T($"Só há {stock} em estoque.", $"Only {stock} in stock."));

        var (unit, category) = pricing.Asked(listing);
        HaggleResult? haggle = null;
        if (request.Haggle is { } h)
        {
            haggle = pricing.Haggle(h, dice, category, request.Quantity);
            // Fixers of Operator 4+ see through an undisguised damage before closing the deal (IR5 p.5).
            if (listing.ConditionHidden && listing.Condition != ItemCondition.Normal && h.Buyer.Rank >= rules.Document.Condition.DetectRank.Value)
                change.Notices.Add(L.T($"Com Operator {h.Buyer.Rank}, o comprador percebe que o item está {listing.Condition} ({rules.Document.Condition.DetectRank.Source}), a não ser que o dano tenha sido disfarçado.", $"With Operator {h.Buyer.Rank}, the buyer notices the item is {listing.Condition} ({rules.Document.Condition.DetectRank.Source}), unless the damage was disguised."));
        }
        var outcome = pricing.Apply(haggle, request.DealId, unit, category, request.Quantity);
        var delivered = outcome.Delivered;
        if (listing.Stock is { } s2 && delivered > s2) delivered = s2; // the free unit only if there is one left

        Replace(state, listing with { Stock = listing.Stock is { } s3 ? s3 - delivered : null });
        var buyer = string.IsNullOrWhiteSpace(request.Buyer) ? null : request.Buyer.Trim();
        state.Sales.Add(new MarketSale
        {
            Id = NextId(change), At = change.Now, ListingId = listing.Id, Name = Label(listing), Buyer = buyer,
            Quantity = request.Quantity, Delivered = delivered, UnitPrice = unit, Total = outcome.Total, PaidNow = outcome.PaidNow,
            DueLater = outcome.DueLater, DealId = outcome.DealId, Haggle = haggle?.Summary,
        });
        change.Log("sale", L.T($"{buyer ?? "Cliente"} comprou {delivered}× {Label(listing)}", $"{buyer ?? "Customer"} bought {delivered}× {Label(listing)}"),
            L.T($"{outcome.Total}eb ({outcome.Description}){(outcome.DueLater > 0 ? $" Faltam {outcome.DueLater}eb em um mês." : "")} Retirar no booth {listing.Stall}.", $"{outcome.Total}eb ({outcome.Description}){(outcome.DueLater > 0 ? $" {outcome.DueLater}eb due in a month." : "")} Pick up at booth {listing.Stall}."));
        if (listing.CategoryKey == "cyberware" && !IsFashionware(change, listing))
            change.Notices.Add(rules.Document.Generator.CyberwareInstall.Value);
    }

    public void UndoSale(MarketChange change, int saleId)
    {
        var state = change.State;
        var index = state.Sales.FindIndex(s => s.Id == saleId);
        if (index < 0) throw new NotFoundException(L.T($"Venda {saleId} não encontrada.", $"Sale {saleId} not found."));
        var sale = state.Sales[index];
        if (sale.Undone) throw new ConflictException(L.T("Esta venda já foi desfeita.", "This sale was already undone."));
        state.Sales[index] = sale with { Undone = true };
        if (sale.ListingId is { } id && state.Listings.FirstOrDefault(l => l.Id == id) is { Stock: { } stock } listing)
            Replace(state, listing with { Stock = stock + sale.Delivered });
        change.Log("undo", L.T($"Venda desfeita: {sale.Delivered}× {sale.Name}", $"Sale undone: {sale.Delivered}× {sale.Name}"));
    }

    // ------------------------------------------------------------------ listings

    public void AddListing(MarketChange change, AddListingRequest request)
    {
        var build = change.Builder(rules);
        var stall = StallFor(change, build, request.Stall);
        MarketListing added;
        if (request.ItemId is { } itemId)
        {
            var item = change.Catalog.Find(itemId) ?? throw new NotFoundException(L.T($"Item {itemId} não encontrado.", $"Item {itemId} not found."));
            var listed = build.Item(item, stall, ListingOrigin.Manual, L.T("Adicionado pelo Mestre", "Added by the GM"), null, rolled: false, quality: request.Quality);
            added = listed.FirstOrDefault() ?? throw new ConflictException(L.T($"{item.Name} já está à venda neste mercado.", $"{item.Name} is already for sale at this market."));
        }
        else if (request.IndexRow is { } row)
        {
            if (row < 0 || row >= rules.Index.Count) throw new NotFoundException(L.T($"Linha {row} do Night Market Index não existe.", $"Night Market Index row {row} doesn't exist."));
            var entry = rules.Index[row];
            if (entry.Slug is { } slug && change.Catalog.Find(slug) is { } item)
            {
                AddListing(change, request with { ItemId = item.Id, IndexRow = null });
                return;
            }
            if (entry.PriceEb is not { } price)
                throw RequestValidationException.For("indexRow", L.T($"{entry.Name} não tem preço fixo no índice ({entry.Cost}).", $"{entry.Name} has no fixed price in the index ({entry.Cost})."));
            added = build.Local(entry.Name, price, stall, ListingOrigin.Index, "Night Market Index", null,
                source: string.Join(", ", entry.Sources.Select(s => s.Page is { } p ? $"{s.Book} {p}" : s.Book)),
                note: L.T("Só no Night Market Index: sem ficha no catálogo.", "Night Market Index only: no catalog page."));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price is not ({ } price and >= 0))
                throw RequestValidationException.For("name", L.T("Informe um item do catálogo, uma linha do índice ou nome e preço.", "Enter a catalog item, an index row or a name and price."));
            added = build.Local(request.Name.Trim(), price, stall, ListingOrigin.Manual, L.T("Adicionado pelo Mestre", "Added by the GM"), null, note: request.Note);
        }
        change.Commit(build);
        Replace(change.State, added with
        {
            Condition = request.Condition, ConditionHidden = request.ConditionHidden, Stock = Stock(request.Stock),
            Note = request.Note ?? added.Note,
        });
        change.Log("listing", L.T($"Listagem adicionada: {added.Name}", $"Listing added: {added.Name}"), $"Booth {added.Stall}.");
    }

    public void UpdateListing(MarketChange change, int listingId, UpdateListingRequest request)
    {
        var listing = Listing(change.State, listingId);
        var build = change.Builder(rules);
        var stall = request.Stall is null ? listing.Stall : StallFor(change, build, request.Stall).Code;
        change.Commit(build);
        if (request.Condition == ItemCondition.Normal && request.ConditionHidden)
            throw RequestValidationException.For("conditionHidden", L.T("Só há o que esconder num item Damaged ou Destroyed.", "There's only something to hide on a Damaged or Destroyed item."));
        Replace(change.State, listing with
        {
            Stall = stall, Condition = request.Condition, ConditionHidden = request.ConditionHidden, Stock = Stock(request.Stock),
            ForSale = request.ForSale,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        });
    }

    public void SetPrice(MarketChange change, int listingId, int price)
    {
        if (price < 0) throw RequestValidationException.For("price", L.T("O preço não pode ser negativo.", "The price can't be negative."));
        var listing = Listing(change.State, listingId);
        Replace(change.State, listing with { BasePrice = price, BaseCategory = rules.CategoryOf(price), ForSale = true });
        change.Log("price", L.T($"Preço de {listing.Name}: {price}eb", $"Price of {listing.Name}: {price}eb"), L.T("Preço combinado na mesa (CP:R p.385: tudo vale o que o comprador paga).", "Price agreed at the table (CP:R p.385: everything is worth what the buyer pays)."));
    }

    public void RemoveListing(MarketChange change, int listingId)
    {
        var listing = Listing(change.State, listingId);
        change.State.Listings.Remove(listing);
        change.Log("listing", L.T($"Listagem removida: {listing.Name}", $"Listing removed: {listing.Name}"));
    }

    /// <summary>Fills a "GM's choice" row or a Midnight Market slot with a catalog item, picked or drawn.</summary>
    public void FillSlot(MarketChange change, int slotId, FillSlotRequest request)
    {
        var state = change.State;
        var slot = state.OpenSlots.FirstOrDefault(s => s.Id == slotId) ?? throw new NotFoundException(L.T($"Espaço {slotId} não encontrado.", $"Slot {slotId} not found."));
        MarketItem item;
        if (request.ItemId is { } id)
            item = change.Catalog.Find(id) ?? throw new NotFoundException(L.T($"Item {id} não encontrado.", $"Item {id} not found."));
        else if (request.Draw)
        {
            var pool = DrawPool(change, slot);
            if (pool.Count == 0) throw RequestValidationException.For("draw", L.T("Nenhum item do acervo atende a esta linha.", "No item in the collection fits this line."));
            item = pool[dice.Roll(pool.Count) - 1];
            change.Notices.Add(L.T($"Sorteado entre {pool.Count} itens: {item.Name}. {rules.Document.Generator.GmChoiceDraw.Reference}", $"Drawn from {pool.Count} items: {item.Name}. {rules.Document.Generator.GmChoiceDraw.Reference}"));
        }
        else throw RequestValidationException.For("itemId", L.T("Escolha um item ou peça o sorteio.", "Pick an item or ask for a draw."));

        var build = change.Builder(rules);
        var stall = state.Stalls.FirstOrDefault(s => s.Code == slot.Stall) ?? build.NewStallFor(slot.Label);
        var listed = build.Item(item, stall, slot.Origin, slot.Label, slot.GoodsId, rolled: slot.Origin != ListingOrigin.Midnight);
        if (listed.Count == 0) throw new ConflictException(L.T($"{item.Name} já está à venda neste mercado.", $"{item.Name} is already for sale at this market."));
        change.Commit(build);
        state.OpenSlots.Remove(slot);
        change.Log("slot", $"{slot.Label}: {item.Name}");
    }

    private List<MarketItem> DrawPool(MarketChange change, OpenSlot slot)
    {
        var books = change.State.Books.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<MarketItem> pool;
        if (slot.Origin == ListingOrigin.Midnight)
        {
            var draw = rules.Document.MidnightMarket.DrawPool.Value;
            pool = change.Catalog.Items.Where(i => i.Price is not null && (i.PriceCategory >= draw.MinCategory || draw.OrExotic && i.Exotic));
        }
        else pool = change.Catalog.Query(slot.Query, books);
        var build = change.Builder(rules);
        return pool.Where(i => i.Price is not null && !build.Has(i.Id, null)).OrderBy(i => i.Id).ToList();
    }

    public void AddMidnight(MarketChange change)
    {
        var state = change.State;
        var rank = state.Organizer.OperatorRank;
        var op = rules.Document.Operator;
        if (rank is { } r && r < op.MidnightMarketRank.Value)
            throw RequestValidationException.For("midnight", L.T($"Só um Fixer de Operator {op.MidnightMarketRank.Value} ou mais monta um Midnight Market ({op.MidnightMarketRank.Source}).", $"Only a Fixer with Operator {op.MidnightMarketRank.Value} or higher sets up a Midnight Market ({op.MidnightMarketRank.Source})."));
        if (state.OpenSlots.Any(s => s.Origin == ListingOrigin.Midnight) || state.Listings.Any(l => l.Origin == ListingOrigin.Midnight))
            throw new ConflictException(L.T("Este mercado já tem um Midnight Market.", "This market already has a Midnight Market."));
        var build = change.Builder(rules);
        var count = dice.Roll(10) + 5;
        var stall = build.NewStallFor("Midnight Market");
        for (var i = 1; i <= count; i++)
            build.Slot(stall, L.T($"Item raro {i} (à escolha do Mestre)", $"Rare item {i} (GM's choice)"), new ItemQuery(), ListingOrigin.Midnight, null);
        change.Commit(build);
        change.Log("midnight", L.T($"Midnight Market: {count} itens raros", $"Midnight Market: {count} rare items"), $"{rules.Document.MidnightMarket.Items.Value} ({rules.Document.MidnightMarket.Items.Source}).");
    }

    // ------------------------------------------------------------------ events

    public DoorPrizeResult DoorPrize(MarketChange change, DoorPrizeRequest request)
    {
        var rulesDp = rules.Document.DoorPrize;
        var wheel = new[] { rulesDp.Example.Value }.Concat(rulesDp.Slices.Value).ToList();
        var total = wheel.Sum(s => s.Weight);
        if (request.Roll is < 1 || request.Roll > total) throw RequestValidationException.For("roll", L.T($"A roleta vai de 1 a {total}.", $"The wheel goes from 1 to {total}."));
        var roll = request.Roll ?? dice.Roll(total);
        var acc = 0;
        var slice = wheel.First(s => (acc += s.Weight) >= roll);
        var prize = Prize(change.Catalog, slice);
        var bonus = rulesDp.Bonus.Value.Select(b => Prize(change.Catalog, b)).ToList();
        var winner = string.IsNullOrWhiteSpace(request.Winner) ? "Visitante" : request.Winner.Trim();
        foreach (var p in bonus.Prepend(prize))
            change.State.Sales.Add(new MarketSale
            {
                Id = NextId(change), At = change.Now, Name = p.Label, Buyer = winner, Quantity = p.Quantity, Delivered = p.Quantity, Kind = "prize",
            });
        change.Log("door-prize", $"Door Prize: {winner} ganhou {prize.Label}",
            L.T($"Roleta {roll}/{total}. Mais: {string.Join(" e ", bonus.Select(b => b.Label))}. {rulesDp.Slices.Reference}", $"Wheel {roll}/{total}. Also: {string.Join(" and ", bonus.Select(b => L.D(b.Label)))}. {rulesDp.Slices.Reference}"));
        return new DoorPrizeResult(roll, total, prize, bonus);
    }

    public MysteryBoxResult MysteryBox(MysteryBoxRollRequest request, MarketCatalog catalog)
    {
        var boxes = rules.Document.MysteryBoxes.Boxes;
        if (request.BoxRoll is < 1 or > 10) throw RequestValidationException.For("boxRoll", L.T("A Table of Mystery é um 1d10.", "The Table of Mystery is a 1d10."));
        if (request.ContentRoll is < 1 or > 6) throw RequestValidationException.For("contentRoll", L.T("O conteúdo é um 1d6.", "The contents are a 1d6."));
        var boxRoll = request.BoxRoll ?? dice.Roll(10);
        var box = boxes.Value.First(b => b.Roll == boxRoll);
        var contentRoll = request.ContentRoll ?? dice.Roll(6);
        var content = box.Contents.First(c => c.Roll == contentRoll);
        return new MysteryBoxResult(boxRoll, box.Name, contentRoll, Prize(catalog, content), boxes.Source!);
    }

    public MysteryBoxResult MysteryBox(MarketChange change, MysteryBoxRequest request)
    {
        var result = MysteryBox(new MysteryBoxRollRequest(request.BoxRoll, request.ContentRoll), change.Catalog);
        var recipient = string.IsNullOrWhiteSpace(request.Recipient) ? "Grupo" : request.Recipient.Trim();
        change.State.Sales.Add(new MarketSale
        {
            Id = NextId(change), At = change.Now, Name = result.Prize.Label, Buyer = recipient,
            Quantity = result.Prize.Quantity, Delivered = result.Prize.Quantity, Kind = "box",
        });
        change.Log("mystery-box", L.T($"Mystery Box para {recipient}: {result.Prize.Label}", $"Mystery Box for {recipient}: {L.D(result.Prize.Label)}"), $"{result.Box} (1d10 {result.BoxRoll}, 1d6 {result.ContentRoll}; {result.Source}).");
        return result;
    }

    /// <summary>Counts arrivals; with a Door Prize, every 50th visitor wins (complementary reading of BC+ p.2).</summary>
    public void Visitors(MarketChange change, int count)
    {
        if (count is < 1 or > 10000) throw RequestValidationException.For("count", L.T("Informe de 1 a 10.000 visitantes.", "Enter 1 to 10,000 visitors."));
        var state = change.State;
        var every = rules.Document.DoorPrize.Arrival.Value;
        var before = state.Visitors;
        change.State = state with { Visitors = before + count };
        if (!rules.Preset(state.PresetId).Features.Contains(MarketFeature.DoorPrize)) return;
        for (var n = (before / every + 1) * every; n <= before + count; n += every)
            change.Notices.Add(L.T($"Visitante nº {n}: hora de girar a roleta do Door Prize.", $"Visitor no. {n}: time to spin the Door Prize wheel."));
    }

    // ------------------------------------------------------------------ helpers

    private static PrizeResult Prize(MarketCatalog catalog, PrizeEntry entry)
    {
        var item = catalog.Find(entry.Slug);
        var extra = catalog.Find(entry.ExtraSlug);
        return new PrizeResult(entry.Roll, entry.Label, entry.Quantity, item?.Id, item?.Name, extra?.Id, extra?.Name, entry.Loaded, entry.Quality);
    }

    private MarketStall StallFor(MarketChange change, ListingBuilder build, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return change.State.Stalls.FirstOrDefault() ?? build.NewStall("Banca");
        var existing = change.State.Stalls.FirstOrDefault(s => string.Equals(s.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var trimmed = code.Trim();
        var digits = trimmed.StartsWith(change.State.Letter, StringComparison.OrdinalIgnoreCase) ? trimmed[change.State.Letter.Length..] : trimmed;
        if (!int.TryParse(digits, out var number) || number is < 0 or > 99)
            throw RequestValidationException.For("stall", L.T($"Booth '{code}' inválido: use {change.State.Letter} seguido do número (ex.: {change.State.Letter}3).", $"Invalid booth '{code}': use {change.State.Letter} followed by the number (e.g. {change.State.Letter}3)."));
        return build.Stall(number, $"Booth {change.State.Letter}{number}");
    }

    private static int? Stock(int? stock) => stock is < 0
        ? throw RequestValidationException.For("stock", L.T("O estoque não pode ser negativo.", "Stock can't be negative."))
        : stock;

    private static MarketListing Listing(MarketState state, int id) =>
        state.Listings.FirstOrDefault(l => l.Id == id) ?? throw new NotFoundException(L.T($"Listagem {id} não encontrada.", $"Listing {id} not found."));

    private static void Replace(MarketState state, MarketListing listing)
    {
        var index = state.Listings.FindIndex(l => l.Id == listing.Id);
        state.Listings[index] = listing;
    }

    private static int NextId(MarketChange change)
    {
        var id = change.State.NextId;
        change.State = change.State with { NextId = id + 1 };
        return id;
    }

    private static string Label(MarketListing l) =>
        l.Quality is { } q && q != WeaponQuality.Standard ? $"{l.Name} ({(q == WeaponQuality.Poor ? "Poor" : "Excellent")} Quality)" : l.Name;

    private static bool IsFashionware(MarketChange change, MarketListing l) =>
        change.Catalog.Find(l.ItemId) is { Type: "Fashionware" };
}
