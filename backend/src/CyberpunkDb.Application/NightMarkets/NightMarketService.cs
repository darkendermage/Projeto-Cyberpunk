using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets.Generation;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Application.NightMarkets.Trading;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.NightMarkets;

/// <summary>
/// Saved Night Markets: generation, the CBK listings with prices recalculated on every read, purchases to the
/// market's own ledger and the GM events. Every write carries the market version (409 on a stale one).
/// </summary>
public sealed class NightMarketService(
    IAppDbContext db,
    NightMarketRuleBook rules,
    MarketCatalogProvider catalogs,
    NightMarketGenerator generator,
    MarketOperations operations,
    MarketPricing pricing,
    IDiceRoller dice)
{
    internal static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Dictionary<string, string> SectionLabels = new()
    {
        ["weapons"] = "Weapons, Ammo & Attachments", ["armor"] = "Armor", ["gear"] = "Gear", ["cyberware"] = "Cyberware",
        ["fashion"] = "Fashion", ["vehicles"] = "Vehicles", ["net"] = "NET Stuff", ["lifestyle"] = "Lifestyle & Housing",
        ["entertainment"] = "Entertainment & Services", ["elflines"] = "Elflines Online",
    };

    // ------------------------------------------------------------------ reference

    public async Task<NightMarketRulesDto> GetRulesAsync(CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var books = catalog.Items.Where(i => i.Book is not null).GroupBy(i => i.Book!)
            .Select(g => new BookOption(g.Key, g.Count())).OrderByDescending(b => b.Items).ToList();
        var sections = rules.Index.GroupBy(e => e.Section)
            .Select(g => new IndexSectionDto(g.Key, SectionLabels.GetValueOrDefault(g.Key, g.Key), g.Count())).ToList();
        return new NightMarketRulesDto(rules.Document, rules.Presets.Select(Preset).ToList(), books, sections);
    }

    public async Task<IndexPageDto> GetIndexAsync(IndexQuery query, CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var q = TextNormalizer.Normalize(query.Q ?? "");
        var rows = rules.Index.Select((e, i) => (Entry: e, Row: i, Item: catalog.Find(e.Slug)))
            .Where(x => query.Section is null || x.Entry.Section == query.Section)
            .Where(x => query.Category is null || x.Entry.PriceCategory == query.Category)
            .Where(x => query.Catalogued is null || (x.Item is not null) == query.Catalogued)
            .Where(x => q.Length == 0 || TextNormalizer.Normalize($"{x.Entry.Name} {x.Entry.Group}").Contains(q))
            .ToList();
        var size = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(1, query.Page);
        return new IndexPageDto(rows.Count, page, size, rows.Skip((page - 1) * size).Take(size)
            .Select(x => new IndexRowDto(x.Row, x.Entry.Section, x.Entry.Group, x.Entry.Name, x.Entry.Cost, x.Entry.PriceEb,
                x.Entry.PriceCategory, x.Entry.Sources, x.Item?.Id, x.Item?.Slug))
            .ToList());
    }

    public HaggleResult Haggle(HaggleRequest request) => pricing.Haggle(request, dice);

    public async Task<SellQuote> SellQuoteAsync(SellQuoteRequest request, CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        if (request.ItemId is { } id)
        {
            var item = catalog.Find(id) ?? throw new NotFoundException($"Item {id} não encontrado.");
            if (item.Price is not { } price) throw RequestValidationException.For("itemId", $"{item.Name} não tem preço fixo no livro.");
            return pricing.SellQuote(item.Name, price, item.PriceCategory ?? rules.CategoryOf(price), request, dice);
        }
        if (request.Price is not ({ } p and >= 0)) throw RequestValidationException.For("price", "Informe um item do catálogo ou o preço de mercado.");
        return pricing.SellQuote(null, p, rules.CategoryOf(p), request, dice);
    }

    public async Task<MysteryBoxResult> RollMysteryBoxAsync(MysteryBoxRollRequest request, CancellationToken ct) =>
        operations.MysteryBox(request, await catalogs.GetAsync(ct));

    // ------------------------------------------------------------------ markets

    public async Task<IReadOnlyList<NightMarketSummaryDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.NightMarkets.AsNoTracking().OrderByDescending(m => m.UpdatedAt).ToListAsync(ct);
        return rows.Select(m =>
        {
            var state = Deserialize(m.StateJson);
            var sales = state.Sales.Where(s => !s.Undone && s.Kind == "sale").ToList();
            var presetName = rules.Presets.FirstOrDefault(p => p.Id == m.PresetId)?.Name ?? m.PresetId;
            return new NightMarketSummaryDto(m.Id, m.Name, m.PresetId, presetName, m.Letter, m.Organizer, m.OperatorRank, m.Status,
                state.Listings.Count, sales.Count, sales.Sum(s => s.PaidNow), m.UpdatedAt);
        }).ToList();
    }

    public async Task<NightMarketDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        return Detail(entity, Deserialize(entity.StateJson), await catalogs.GetAsync(ct), []);
    }

    public async Task<NightMarketDetailDto> CreateAsync(GenerateMarketRequest request, CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var state = generator.Generate(request, catalog, now);
        var preset = rules.Preset(state.PresetId);
        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{preset.Name} — {now:dd/MM/yyyy}" : request.Name.Trim();
        var entity = new NightMarket { Name = Truncate(name, 80), CreatedAt = now, Status = NightMarketStatus.Open };
        Apply(entity, state, now);
        db.NightMarkets.Add(entity);
        await db.SaveChangesAsync(ct);
        return Detail(entity, state, catalog, []);
    }

    /// <summary>Rolls the market again with the same options and a new seed; the ledger and events are kept.</summary>
    public Task<NightMarketDetailDto> RegenerateAsync(int id, MarketVersionRequest request, CancellationToken ct) =>
        MutateAsync(id, request.Version, change =>
        {
            var old = change.State;
            var fresh = generator.Generate(new GenerateMarketRequest
            {
                PresetId = old.PresetId, Books = old.Books, Organizer = old.Organizer, Options = old.Options,
                Goods = rules.Preset(old.PresetId).Goods == GoodsMode.Roll ? null : old.Goods.Select(g => g.Id).ToList(),
                Midnight = old.OpenSlots.Any(s => s.Origin == ListingOrigin.Midnight) || old.Listings.Any(l => l.Origin == ListingOrigin.Midnight),
            }, change.Catalog, change.Now);
            var nextId = Math.Max(old.NextId, fresh.NextId);
            change.State = fresh with
            {
                Sales = old.Sales, Events = [.. old.Events, .. fresh.Events], Visitors = old.Visitors, Notes = old.Notes,
                Listings = fresh.Listings.Select(l => l with { Id = l.Id + nextId }).ToList(),
                OpenSlots = fresh.OpenSlots.Select(s => s with { Id = s.Id + nextId }).ToList(),
                NextId = fresh.NextId + nextId,
            };
        }, ct);

    public async Task<NightMarketDetailDto> UpdateAsync(int id, UpdateMarketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw RequestValidationException.For("name", "Dê um nome ao mercado.");
        return await MutateAsync(id, request.Version, change =>
        {
            change.State = change.State with { Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim() };
        }, ct, entity =>
        {
            entity.Name = Truncate(request.Name.Trim(), 80);
            entity.Status = request.Status;
        });
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        db.NightMarkets.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task<NightMarketDetailDto> PurchaseAsync(int id, PurchaseRequest r, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        if (entity.Status == NightMarketStatus.Closed) throw new ConflictException("Este mercado está encerrado. Reabra-o para vender.");
        return await MutateAsync(id, r.Version, c => operations.Purchase(c, r), ct);
    }

    public Task<NightMarketDetailDto> UndoSaleAsync(int id, int saleId, MarketVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.UndoSale(c, saleId), ct);

    public Task<NightMarketDetailDto> AddListingAsync(int id, AddListingRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.AddListing(c, r), ct);

    public Task<NightMarketDetailDto> UpdateListingAsync(int id, int listingId, UpdateListingRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.UpdateListing(c, listingId, r), ct);

    public Task<NightMarketDetailDto> SetPriceAsync(int id, int listingId, SetPriceRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.SetPrice(c, listingId, r.Price), ct);

    public Task<NightMarketDetailDto> RemoveListingAsync(int id, int listingId, MarketVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.RemoveListing(c, listingId), ct);

    public Task<NightMarketDetailDto> FillSlotAsync(int id, int slotId, FillSlotRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.FillSlot(c, slotId, r), ct);

    public Task<NightMarketDetailDto> AddMidnightAsync(int id, MarketVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, operations.AddMidnight, ct);

    public Task<NightMarketDetailDto> VisitorsAsync(int id, VisitorsRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => operations.Visitors(c, r.Count), ct);

    public async Task<(NightMarketDetailDto Market, DoorPrizeResult Prize)> DoorPrizeAsync(int id, DoorPrizeRequest r, CancellationToken ct)
    {
        DoorPrizeResult? result = null;
        var market = await MutateAsync(id, r.Version, c => result = operations.DoorPrize(c, r), ct);
        return (market, result!);
    }

    public async Task<(NightMarketDetailDto Market, MysteryBoxResult Box)> MysteryBoxAsync(int id, MysteryBoxRequest r, CancellationToken ct)
    {
        MysteryBoxResult? result = null;
        var market = await MutateAsync(id, r.Version, c => result = operations.MysteryBox(c, r), ct);
        return (market, result!);
    }

    // ------------------------------------------------------------------ plumbing

    private async Task<NightMarketDetailDto> MutateAsync(int id, int version, Action<MarketChange> operation, CancellationToken ct,
        Action<NightMarket>? columns = null)
    {
        var entity = await FindAsync(id, ct);
        if (entity.Version != version)
            throw new ConflictException("Este mercado foi alterado em outra janela. Recarregue para ver a versão atual.");
        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var change = new MarketChange(Deserialize(entity.StateJson), catalog, now);
        operation(change);
        columns?.Invoke(entity);
        Apply(entity, change.State, now);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Este mercado foi alterado ao mesmo tempo em outra janela. Recarregue e tente de novo.");
        }
        return Detail(entity, change.State, catalog, change.Notices);
    }

    private static void Apply(NightMarket entity, MarketState state, DateTime now)
    {
        entity.PresetId = state.PresetId;
        entity.Letter = state.Letter;
        entity.Organizer = Truncate(state.Organizer.Name, 80);
        entity.OperatorRank = state.Organizer.OperatorRank;
        entity.StateJson = JsonSerializer.Serialize(state, StateJson);
        entity.UpdatedAt = now;
        entity.Version++;
    }

    private NightMarketDetailDto Detail(NightMarket entity, MarketState state, MarketCatalog catalog, IReadOnlyList<string> notices)
    {
        var listings = state.Listings.Select(l => Listing(l, state, catalog)).ToList();
        var stalls = state.Stalls.Select(s => new StallDto(s.Code, s.Number, s.Label,
            state.Listings.Count(l => l.Stall == s.Code) + state.OpenSlots.Count(o => o.Stall == s.Code))).ToList();
        return new NightMarketDetailDto(entity.Id, entity.Name, entity.Version, entity.Status, state.Notes, entity.CreatedAt, entity.UpdatedAt,
            Preset(rules.Preset(state.PresetId)), state.Organizer, rules.Reach(state.Organizer.OperatorRank), state.Seed, state.Books,
            state.Options, state.Goods, stalls, listings, state.OpenSlots, state.Sales, state.Events, state.Visitors, notices);
    }

    private ListingDto Listing(MarketListing l, MarketState state, MarketCatalog catalog)
    {
        var item = catalog.Find(l.ItemId);
        var (price, category) = pricing.Asked(l);
        var (value, valueCategory) = pricing.Value(l);
        return new ListingDto(l.Id, l.Stall, l.ItemId, l.Slug, l.Name, l.CategoryKey, item?.Manufacturer, item?.Description,
            item?.Book, item?.Page, l.BasePrice, l.BaseCategory, price, category, value, valueCategory, l.Quality, l.Condition,
            l.ConditionHidden, l.Stock, l.ForSale, l.Origin, l.Group, l.GoodsId, l.Source, l.Note, pricing.Warnings(l, state.Organizer));
    }

    private static MarketPresetDto Preset(MarketPreset p) => new(p.Id, p.Name, p.Kind, p.Letter, p.Organizer, p.Summary, p.Frequency,
        p.Location, p.Sells, p.Invite, p.Rules, p.Goods, p.SuggestedGoods, p.Features, p.Admission, p.ForSale, p.Source);

    private async Task<NightMarket> FindAsync(int id, CancellationToken ct) =>
        await db.NightMarkets.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException($"Night Market {id} não encontrado.");

    private static MarketState Deserialize(string json) =>
        JsonSerializer.Deserialize<MarketState>(json, StateJson) ?? new MarketState();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
