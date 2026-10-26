using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NightMarkets.Generation;

/// <summary>
/// Builds a market from a preset: the Night Market Generator of CP:R p.338–339 (kinds of goods, 1d10 lines, d100 rows),
/// the Black Chrome listings with the market's booth letter (BC p.7), the preset's own stock, the second-hand
/// condition and the Midnight Market slots. Same seed, same market.
/// </summary>
public sealed class NightMarketGenerator(NightMarketRuleBook rules)
{
    private const string CoreBook = "Core Book";

    public MarketState Generate(GenerateMarketRequest request, MarketCatalog catalog, DateTime now)
    {
        var preset = rules.Preset(request.PresetId);
        var seed = request.Seed ?? Random.Shared.Next(1, int.MaxValue);
        var rng = new Random(seed);
        var organizer = Organizer(preset, request.Organizer);
        var options = request.Options ?? new MarketOptions();
        var midnight = request.Midnight || preset.Features.Contains(MarketFeature.Midnight);
        Validate(organizer, preset, options, request);

        var books = Books(preset, request.Books, catalog);
        var bookSet = books.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stalls = new List<MarketStall>();
        var listings = new List<MarketListing>();
        var slots = new List<OpenSlot>();
        var build = new ListingBuilder(rules, catalog, preset.Letter, stalls, listings, slots, 1);
        foreach (var s in preset.Stalls) build.Stall(s.Number, s.Label);

        // The book's own listings first, so they keep their booth when a d100 row also brings them.
        if ((preset.IsBlackChrome ? preset.Letter : options.BlackChromeLetter) is { } bcLetter)
            AddBlackChrome(bcLetter, build, catalog);

        foreach (var pool in preset.Pools)
        {
            var stall = build.Stall(pool.Stall, preset.Stalls.FirstOrDefault(s => s.Number == pool.Stall)?.Label ?? pool.Label);
            foreach (var item in catalog.Query(pool.Query).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                build.Item(item, stall, ListingOrigin.Preset, pool.Label, null, rolled: false, forSale: pool.ForSale);
        }

        var goods = new List<RolledGoods>();
        foreach (var (good, roll) in ChooseGoods(preset, request, rng))
            goods.Add(Fill(good, roll, request.Lines?.GetValueOrDefault(good.Id), preset, build, catalog, bookSet, rng));

        if (preset.Admission is { } admission)
            build.Local(admission.Label, admission.Price, build.Stall(0, "Entrada"), ListingOrigin.Admission, admission.Label, null,
                source: admission.Source);

        if (options.IncludeRelated)
            AddRelated(goods, build, catalog, bookSet, rng);

        if (midnight)
            AddMidnight(build, rng);

        if (options.DamagedPercent > 0)
            for (var i = 0; i < listings.Count; i++)
                if (listings[i] is { ItemId: not null, BasePrice: > 0 } l && rng.Next(100) < options.DamagedPercent)
                    listings[i] = l with { Condition = ItemCondition.Damaged, ConditionHidden = options.Scam };

        return new MarketState
        {
            PresetId = preset.Id, Letter = preset.Letter, Kind = preset.Kind,
            Organizer = organizer, Seed = seed, Books = books, Options = options, Goods = goods,
            Stalls = stalls, Listings = listings, OpenSlots = slots, NextId = build.NextId,
            Events = [new MarketEvent(now, "generated", L.T($"Mercado gerado (seed {seed})", $"Market generated (seed {seed})"), Describe(goods, listings, slots))],
        };
    }

    // ------------------------------------------------------------------ setup

    private MarketOrganizer Organizer(MarketPreset preset, MarketOrganizer? requested) => new()
    {
        Name = string.IsNullOrWhiteSpace(requested?.Name) ? preset.Organizer.Name : requested.Name.Trim(),
        Role = requested?.Role ?? preset.Organizer.Role,
        OperatorRank = requested?.OperatorRank ?? preset.Organizer.OperatorRank,
        Trading = requested?.Trading ?? preset.Organizer.Trading,
    };

    private void Validate(MarketOrganizer organizer, MarketPreset preset, MarketOptions options, GenerateMarketRequest request)
    {
        var op = rules.Document.Operator;
        if (organizer.OperatorRank is < 0 or > 10)
            throw RequestValidationException.For("organizer.operatorRank", L.T("O Operator Rank vai de 0 a 10.", "Operator Rank goes from 0 to 10."));
        if (preset.Kind == MarketKind.NightMarket && organizer.OperatorRank is { } rank && rank < op.NightMarketRank.Value)
            throw RequestValidationException.For("organizer.operatorRank",
                L.T($"Só um Fixer de Operator {op.NightMarketRank.Value} ou mais organiza um Night Market ({op.NightMarketRank.Source}). Sem isso, ele encontra itens até {rules.Reach(rank)} peça por peça.", $"Only a Fixer with Operator {op.NightMarketRank.Value} or higher organizes a Night Market ({op.NightMarketRank.Source}). Without it, they find items up to {rules.Reach(rank)} piece by piece."));
        if (request.Midnight && organizer.OperatorRank is { } r && r < op.MidnightMarketRank.Value)
            throw RequestValidationException.For("midnight",
                L.T($"Só um Fixer de Operator {op.MidnightMarketRank.Value} ou mais monta um Midnight Market ({op.MidnightMarketRank.Source}).", $"Only a Fixer with Operator {op.MidnightMarketRank.Value} or higher sets up a Midnight Market ({op.MidnightMarketRank.Source})."));
        if (options.DamagedPercent is < 0 or > 100)
            throw RequestValidationException.For("options.damagedPercent", L.T("Use uma chance de 0 a 100%.", "Use a chance from 0 to 100%."));
        if (options.BlackChromeLetter is { } l && (l.Length != 1 || l[0] is < 'A' or > 'F'))
            throw RequestValidationException.For("options.blackChromeLetter", L.T("Os mercados do Black Chrome vão de A a F (BC p.131).", "The Black Chrome markets go from A to F (BC p.131)."));
        if (request.Lines?.Values.Any(v => v is < 1 or > 10) == true)
            throw RequestValidationException.For("lines", L.T("O número de linhas é um 1d10: de 1 a 10.", "The number of lines is a 1d10: from 1 to 10."));
    }

    /// <summary>The generic Core market uses only the Core Rulebook unless the GM enables more books.</summary>
    private static List<string> Books(MarketPreset preset, List<string>? requested, MarketCatalog catalog)
    {
        var all = catalog.Items.Select(i => i.Book).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (requested is { Count: > 0 })
        {
            var unknown = requested.FirstOrDefault(b => !all.Contains(b, StringComparer.OrdinalIgnoreCase));
            if (unknown is not null) throw RequestValidationException.For("books", L.T($"O livro '{unknown}' não está no catálogo.", $"The book '{unknown}' isn't in the catalog."));
            return requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        return preset.Id is "core" ? [CoreBook] : all.OrderBy(b => b, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IEnumerable<(GoodsType Good, int? Roll)> ChooseGoods(MarketPreset preset, GenerateMarketRequest request, Random rng)
    {
        if (request.Goods is { Count: > 0 } picked)
            return picked.Distinct(StringComparer.OrdinalIgnoreCase).Select(id => (rules.Good(id), (int?)null)).ToList();
        if (preset.Goods == GoodsMode.Roll || request.RollGoods) return RollGoods(rng);
        return preset.Goods switch
        {
            GoodsMode.Suggested => preset.SuggestedGoods.Select(id => (rules.Good(id), (int?)null)).ToList(),
            GoodsMode.Pick => throw RequestValidationException.For("goods", L.T("Escolha os tipos de mercadoria ou peça para rolar.", "Pick the types of goods or ask to roll.")),
            _ => [],
        };
    }

    /// <summary>1d6 per kind of goods; a repeated result is rolled again (CP:R p.338).</summary>
    private List<(GoodsType, int?)> RollGoods(Random rng)
    {
        var chosen = new List<(GoodsType, int?)>();
        while (chosen.Count < rules.Document.Generator.GoodsRolls.Value)
        {
            var roll = rng.Next(1, 7);
            if (chosen.Any(c => c.Item2 == roll)) continue;
            chosen.Add((rules.GoodByRoll(roll)!, roll));
        }
        return chosen;
    }

    // ------------------------------------------------------------------ d100 rows

    private RolledGoods Fill(GoodsType good, int? goodsRoll, int? typedLines, MarketPreset preset, ListingBuilder build,
        MarketCatalog catalog, IReadOnlySet<string> books, Random rng)
    {
        var lines = typedLines ?? rng.Next(1, 11);
        var band = rules.Document.Generator.BandSize.Value;
        var stall = StallFor(good, preset, build);
        var rolls = new List<TableRoll>();
        while (rolls.Count < Math.Min(lines, good.Table.Length))
        {
            var roll = rng.Next(1, 101);
            var index = (roll - 1) / band;
            if (rolls.Any(r => r.Band == index)) continue; // same result: roll again (CP:R p.338)
            var entry = good.Table[index];
            rolls.Add(new TableRoll(roll, index, entry.Label));
            Resolve(entry, good, stall, build, catalog, books);
        }
        return new RolledGoods(good.Id, good.Name, goodsRoll, lines, rolls);
    }

    private static MarketStall StallFor(GoodsType good, MarketPreset preset, ListingBuilder build) =>
        preset.Stalls.FirstOrDefault(s => s.Goods.Contains(good.Id)) is { } s
            ? build.Stall(s.Number, s.Label)
            : build.NewStallFor(good.Name);

    private static void Resolve(TableEntry entry, GoodsType good, MarketStall stall, ListingBuilder build, MarketCatalog catalog,
        IReadOnlySet<string> books)
    {
        if (entry.GmChoice)
        {
            build.Slot(stall, entry.Label, entry.Query ?? new ItemQuery(), ListingOrigin.GmChoice, good.Id);
            return;
        }
        if (entry.Local is { } local)
        {
            build.Local(local.Name, local.Price, stall, ListingOrigin.CoreTable, entry.Label, good.Id, local.Category, "CP:R p.338–339", entry.Note);
            return;
        }
        var items = entry.Slugs is { Length: > 0 } slugs
            ? slugs.Select(s => catalog.Find(s)).OfType<MarketItem>().ToList()
            : entry.Query is { } q ? catalog.Query(q, books).OrderBy(i => i.Price).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList() : [];
        if (items.Count == 0)
        {
            // Nothing of this row in the enabled books: leave it for the GM to pick from the whole catalog.
            build.Slot(stall, entry.Label, entry.Query ?? new ItemQuery(), ListingOrigin.CoreTable, good.Id);
            return;
        }
        foreach (var item in items)
            build.Item(item, stall, ListingOrigin.CoreTable, entry.Label, good.Id, rolled: true, note: entry.Note);
    }

    // ------------------------------------------------------------------ other sources

    /// <summary>Every Black Chrome listing with a booth of this letter, at its numbered spot (BC p.7).</summary>
    private void AddBlackChrome(string bcLetter, ListingBuilder build, MarketCatalog catalog)
    {
        var legend = rules.Presets.FirstOrDefault(p => p.IsBlackChrome && p.Letter == bcLetter)?.Stalls ?? [];
        foreach (var (slug, codes) in rules.Booths.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            var code = codes.FirstOrDefault(c => c.StartsWith(bcLetter, StringComparison.Ordinal));
            if (code is null || catalog.Find(slug) is not { } item || !int.TryParse(code[1..], out var number)) continue;
            var stall = build.Stall(number, legend.FirstOrDefault(s => s.Number == number)?.Label ?? $"Booth {code}");
            build.Item(item, stall, ListingOrigin.BlackChrome, "Black Chrome", null, rolled: false);
        }
    }

    /// <summary>For each rolled kind of goods, as many items of the other enabled books as it rolled lines (complementary).</summary>
    private void AddRelated(List<RolledGoods> goods, ListingBuilder build, MarketCatalog catalog, IReadOnlySet<string> books, Random rng)
    {
        var others = books.Where(b => !string.Equals(b, CoreBook, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (others.Count == 0) return;
        foreach (var rolled in goods)
        {
            var good = rules.Good(rolled.Id);
            var stall = build.NewStallFor(good.Name);
            var pool = good.Related.SelectMany(q => catalog.Query(q, others)).DistinctBy(i => i.Id)
                .Where(i => i.Price is not null && !build.Has(i.Id, null)).OrderBy(i => i.Id).ToList();
            for (var n = 0; n < rolled.Lines && pool.Count > 0; n++)
            {
                var pick = pool[rng.Next(pool.Count)];
                pool.Remove(pick);
                build.Item(pick, stall, ListingOrigin.Related, L.T("Outros livros", "Other books"), good.Id, rolled: true);
            }
        }
    }

    /// <summary>1d10 + 5 rare items of the GM's choice (CP:R p.339), each an open slot.</summary>
    private void AddMidnight(ListingBuilder build, Random rng)
    {
        var count = rng.Next(1, 11) + 5;
        var stall = build.NewStallFor("Midnight Market");
        for (var i = 1; i <= count; i++)
            build.Slot(stall, L.T($"Item raro {i} (à escolha do Mestre)", $"Rare item {i} (GM's choice)"), new ItemQuery(), ListingOrigin.Midnight, null);
    }

    private static string Describe(List<RolledGoods> goods, List<MarketListing> listings, List<OpenSlot> slots)
    {
        var parts = goods.Select(g => L.T($"{g.Name}{(g.Roll is { } r ? $" (d6 {r})" : "")}: {g.Lines} linha(s), d100 {string.Join(", ", g.Rolls.Select(x => x.Roll))}", $"{g.Name}{(g.Roll is { } r2 ? $" (d6 {r2})" : "")}: {g.Lines} line(s), d100 {string.Join(", ", g.Rolls.Select(x => x.Roll))}"));
        var summary = string.Join(" · ", parts);
        return L.T($"{(summary.Length > 0 ? summary + ". " : "")}{listings.Count} listagens, {slots.Count} espaço(s) para o Mestre.", $"{(summary.Length > 0 ? summary + ". " : "")}{listings.Count} listings, {slots.Count} slot(s) for the GM.");
    }
}
