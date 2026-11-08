using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// The store sorted by column, and the refund of a purchase during and after creation
[Collection(ApiCollection.Name)]
public class CharacterStoreAndRefundApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> Raw(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        return await _client.SendAsync(request);
    }

    private async Task<CharacterDetailDto> Send(HttpMethod method, string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Raw(method, url, body);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>(Json))!;
    }

    private Task<CharacterDetailDto> Post(CharacterDetailDto c, string path, object body) => Send(HttpMethod.Post, $"/api/characters/{c.Id}/{path}", body);

    private async Task<int> ItemId(string slug)
    {
        var page = (await _client.GetFromJsonAsync<StorePageDto>($"/api/characters/store?q={slug}&pageSize=50", Json))!;
        return page.Items.Single(i => i.Slug == slug).Id;
    }

    private async Task<CharacterDetailDto> FinalizedSolo(string name)
    {
        var c = await Send(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" }, HttpStatusCode.Created);
        c = await Post(c, "creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Send(HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with
        {
            Creation = c.Sheet.Creation with { StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 } },
        }));
        return await Post(c, "creation/finalize", new VersionRequest(c.Version));
    }

    [Fact]
    public async Task The_store_sorts_the_whole_catalog_by_price_name_or_category_before_paging()
    {
        var cheap = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store?category=Weapon&sort=Price&pageSize=1000", Json))!;
        cheap.Items.Select(i => i.Price).Should().BeInAscendingOrder();
        var dear = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store?category=Weapon&sort=Price&desc=true&pageSize=12", Json))!;
        dear.Items[0].Price.Should().Be(cheap.Items[^1].Price, "the first page of the descending sort starts at the most expensive item");
        var byName = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store?sort=Name&pageSize=1000", Json))!;
        byName.Items.Select(i => i.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        var byCategory = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store?sort=Category&desc=true&pageSize=1000", Json))!;
        byCategory.Items.Select(i => i.Category).Should().BeInDescendingOrder();
        byCategory.Total.Should().Be(byName.Total, "sorting never drops items");
        var standard = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store", Json))!;
        standard.PageSize.Should().Be(12);
    }

    [Fact]
    public async Task After_creation_a_purchase_paid_from_the_balance_is_refunded_in_full()
    {
        var c = await FinalizedSolo("Reembolso");
        var balance = c.Sheet.Wallet.Balance;
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, await ItemId("agent")));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, await ItemId("basic-ammo"), 2, Caliber.Rifle));
        c.Sheet.Wallet.Balance.Should().Be(balance - 100 - 20);

        var agentId = await ItemId("agent");
        var agent = c.Sheet.Inventory.Single(i => i.Origin == ItemOrigin.Purchase && i.ItemId == agentId);
        c = await Post(c, "purchase/undo", new InstanceRequest(c.Version, agent.InstanceId));
        var ammo = c.Sheet.Inventory.Single(i => i.Origin == ItemOrigin.Purchase && i.Caliber == Caliber.Rifle);
        c = await Post(c, "purchase/undo", new InstanceRequest(c.Version, ammo.InstanceId));

        c.Sheet.Wallet.Balance.Should().Be(balance);
        c.Sheet.Inventory.Should().NotContain(i => i.InstanceId == agent.InstanceId || i.InstanceId == ammo.InstanceId);
    }

    [Fact]
    public async Task After_creation_refunded_cyberware_gives_back_money_and_humanity()
    {
        var c = await FinalizedSolo("Cromo devolvido");
        var balance = c.Sheet.Wallet.Balance;
        var lost = c.Report.Humanity.Lost;
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, await ItemId("cybereye"), Install: true));
        var link = c.Sheet.Cyberware.Single(x => x.Origin == ItemOrigin.Purchase);
        c.Report.Humanity.Lost.Should().BeGreaterThan(lost);

        c = await Post(c, "purchase/undo", new InstanceRequest(c.Version, link.InstanceId));
        c.Sheet.Wallet.Balance.Should().Be(balance);
        c.Report.Humanity.Lost.Should().Be(lost);
        c.Sheet.Cyberware.Should().NotContain(x => x.InstanceId == link.InstanceId);
    }

    [Fact]
    public async Task After_creation_used_or_given_items_are_not_refunded()
    {
        var c = await FinalizedSolo("Sem reembolso");
        var starting = c.Sheet.Inventory.First(i => i.Origin != ItemOrigin.Purchase);
        (await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/purchase/undo", new InstanceRequest(c.Version, starting.InstanceId)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the starting package was never bought");

        // Damaged armor: take off the starting helmet, buy Kevlar for the head and let it soak a hit.
        foreach (var worn in c.Sheet.Inventory.Where(i => i.Worn == ArmorSlot.Head).ToList())
            c = await Post(c, "equip", new EquipRequest(c.Version, worn.InstanceId, ItemState.Carried));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, await ItemId("kevlar"), Equip: true, Worn: ArmorSlot.Head));
        var kevlar = c.Sheet.Inventory.Single(i => i.Origin == ItemOrigin.Purchase && i.Worn == ArmorSlot.Head);
        c = await Post(c, "armor/ablate", new AblateRequest(c.Version, ArmorSlot.Head));
        var refused = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/purchase/undo", new InstanceRequest(c.Version, kevlar.InstanceId));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("SP 6/7");
    }

    [Fact]
    public async Task A_draft_with_an_origin_but_no_language_asks_only_for_the_language()
    {
        var c = await Send(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = "Só origem", Method = CreationMethod.Streetrat, Role = "Solo", CulturalRegion = "Western European" }, HttpStatusCode.Created);
        c.Sheet.Lifepath.CulturalRegion.Should().Be("Western European");
        var issue = c.Validation.Issues.Single(i => i.Code == "NATIVE_LANGUAGE_REQUIRED");
        issue.Message.Should().Be("Escolha o idioma nativo.");
        issue.Suggestion.Should().Contain("Western European");
    }
}
