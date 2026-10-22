using CyberpunkDb.Application.NightMarkets;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// Night Markets: the Core generator, the Black Chrome and Interface RED markets, the CBK listings, Haggle and the
/// market events. Every change is a command validated on the server and answered with the updated market.
/// </summary>
[ApiController]
[Route("api/night-markets")]
public sealed class NightMarketsController(NightMarketService markets) : ControllerBase
{
    [HttpGet("rules")]
    public Task<NightMarketRulesDto> GetRules(CancellationToken ct) => markets.GetRulesAsync(ct);

    /// <summary>The Night Market Index (v1.1), each row linked to the catalog when the item is there.</summary>
    [HttpGet("index")]
    public Task<IndexPageDto> Index([FromQuery] IndexQuery query, CancellationToken ct) => markets.GetIndexAsync(query, ct);

    /// <summary>A Haggle opposed check without a market (preview before a purchase or a sale).</summary>
    [HttpPost("haggle")]
    public HaggleResult Haggle(HaggleRequest request) => markets.Haggle(request);

    [HttpPost("sell-quote")]
    public Task<SellQuote> SellQuote(SellQuoteRequest request, CancellationToken ct) => markets.SellQuoteAsync(request, ct);

    /// <summary>Opens a Mystery Box redeemed with an Achievement, outside any market (IR4 p.24).</summary>
    [HttpPost("mystery-box")]
    public Task<MysteryBoxResult> MysteryBox(MysteryBoxRollRequest request, CancellationToken ct) => markets.RollMysteryBoxAsync(request, ct);

    [HttpGet]
    public Task<IReadOnlyList<NightMarketSummaryDto>> List(CancellationToken ct) => markets.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<NightMarketDetailDto> Get(int id, CancellationToken ct) => markets.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<NightMarketDetailDto>> Create(GenerateMarketRequest request, CancellationToken ct)
    {
        var created = await markets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<NightMarketDetailDto> Update(int id, UpdateMarketRequest request, CancellationToken ct) => markets.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await markets.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/regenerate")]
    public Task<NightMarketDetailDto> Regenerate(int id, MarketVersionRequest r, CancellationToken ct) => markets.RegenerateAsync(id, r, ct);

    [HttpPost("{id:int}/purchase")]
    public Task<NightMarketDetailDto> Purchase(int id, PurchaseRequest r, CancellationToken ct) => markets.PurchaseAsync(id, r, ct);

    [HttpPost("{id:int}/sales/{saleId:int}/undo")]
    public Task<NightMarketDetailDto> UndoSale(int id, int saleId, MarketVersionRequest r, CancellationToken ct) =>
        markets.UndoSaleAsync(id, saleId, r, ct);

    [HttpPost("{id:int}/listings")]
    public Task<NightMarketDetailDto> AddListing(int id, AddListingRequest r, CancellationToken ct) => markets.AddListingAsync(id, r, ct);

    [HttpPut("{id:int}/listings/{listingId:int}")]
    public Task<NightMarketDetailDto> UpdateListing(int id, int listingId, UpdateListingRequest r, CancellationToken ct) =>
        markets.UpdateListingAsync(id, listingId, r, ct);

    [HttpPost("{id:int}/listings/{listingId:int}/price")]
    public Task<NightMarketDetailDto> SetPrice(int id, int listingId, SetPriceRequest r, CancellationToken ct) =>
        markets.SetPriceAsync(id, listingId, r, ct);

    [HttpPost("{id:int}/listings/{listingId:int}/remove")]
    public Task<NightMarketDetailDto> RemoveListing(int id, int listingId, MarketVersionRequest r, CancellationToken ct) =>
        markets.RemoveListingAsync(id, listingId, r, ct);

    [HttpPost("{id:int}/slots/{slotId:int}/fill")]
    public Task<NightMarketDetailDto> FillSlot(int id, int slotId, FillSlotRequest r, CancellationToken ct) =>
        markets.FillSlotAsync(id, slotId, r, ct);

    [HttpPost("{id:int}/midnight")]
    public Task<NightMarketDetailDto> AddMidnight(int id, MarketVersionRequest r, CancellationToken ct) => markets.AddMidnightAsync(id, r, ct);

    [HttpPost("{id:int}/visitors")]
    public Task<NightMarketDetailDto> Visitors(int id, VisitorsRequest r, CancellationToken ct) => markets.VisitorsAsync(id, r, ct);

    [HttpPost("{id:int}/door-prize")]
    public async Task<MarketEventResult<DoorPrizeResult>> DoorPrize(int id, DoorPrizeRequest r, CancellationToken ct)
    {
        var (market, prize) = await markets.DoorPrizeAsync(id, r, ct);
        return new MarketEventResult<DoorPrizeResult>(market, prize);
    }

    [HttpPost("{id:int}/mystery-box")]
    public async Task<MarketEventResult<MysteryBoxResult>> MysteryBox(int id, MysteryBoxRequest r, CancellationToken ct)
    {
        var (market, box) = await markets.MysteryBoxAsync(id, r, ct);
        return new MarketEventResult<MysteryBoxResult>(market, box);
    }
}
