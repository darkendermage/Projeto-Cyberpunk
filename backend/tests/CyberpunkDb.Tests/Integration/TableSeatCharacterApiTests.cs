using System.Net;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// The player's own sheet through the tunnel: only the seat's character, never the rest of the API
[Collection(ApiCollection.Name)]
public class TableSeatCharacterApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<(SeatDto Seat, CharacterDetailDto Own, CharacterDetailDto Other)> Seated(string name)
    {
        var own = await _t.FinalizedSolo(name);
        var other = await _t.FinalizedSolo($"{name} (outro)");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Lugar {name}", own.Id), expected: HttpStatusCode.Created);
        return (seat, own, other);
    }

    [Fact]
    public async Task Own_character_get_and_equip_pass_on_player_port()
    {
        var (seat, own, _) = await Seated("Popup");

        var detail = await _t.Send<CharacterDetailDto>(_t.Player, HttpMethod.Get, $"/api/characters/{own.Id}", seat: seat.Token);
        var armor = detail.Report.Armor.Body.InstanceId!;
        var after = await _t.Send<CharacterDetailDto>(_t.Player, HttpMethod.Post, $"/api/characters/{own.Id}/equip",
            new EquipRequest(detail.Version, armor, ItemState.Stored), seat.Token);

        after.Report.Armor.Body.Sp.Should().Be(0);
        (await _t.Raw(_t.Player, HttpMethod.Get, "/api/characters/rules", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/characters/{own.Id}/events", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Store_read_passes_with_seat()
    {
        var (seat, _, _) = await Seated("Loja");

        (await _t.Raw(_t.Player, HttpMethod.Get, "/api/characters/store?q=pistol&page=1&pageSize=12", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Item_page_reads_pass_on_player_port()
    {
        // The Gear tab opens an item's catalog page: the item, the category list and its schema.
        var (seat, own, _) = await Seated("PaginaItem");
        var itemId = own.Report.Inventory.First().ItemId;
        var item = await _t.Send<System.Text.Json.Nodes.JsonObject>(_t.Gm, HttpMethod.Get, $"/api/items/{itemId}");
        var category = item["item"]!["category"]!.GetValue<string>();

        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/items/{itemId}", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _t.Raw(_t.Player, HttpMethod.Get, "/api/categories", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/categories/{category}/schema", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/catalog/weapons/search", new { }, seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Player, HttpMethod.Get, "/api/favorites/ids", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/characters/{other}")]
    [InlineData("/api/characters/{own}/../{other}")]
    [InlineData("/api/characters/%2E%2E/{other}")]
    [InlineData("/API/CHARACTERS/{other}")]
    [InlineData("/api/characters")]
    [InlineData("/api/catalog/weapons")]
    [InlineData("/api/encounters")]
    public async Task Guard_refuses_other_character_paths(string template)
    {
        var (seat, own, other) = await Seated($"Guarda{Guid.NewGuid():N}"[..14]);
        var url = template.Replace("{own}", own.Id.ToString()).Replace("{other}", other.Id.ToString());

        (await _t.Raw(_t.Player, HttpMethod.Get, url, seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound, url);
    }

    [Fact]
    public async Task Delete_duplicate_export_import_refused_on_player_port()
    {
        var (seat, own, _) = await Seated("Proibido");

        (await _t.Raw(_t.Player, HttpMethod.Delete, $"/api/characters/{own.Id}", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Player, HttpMethod.Post, $"/api/characters/{own.Id}/duplicate", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/characters/{own.Id}/export", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/characters/import", new { }, seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/characters", new { name = "x" }, seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{own.Id}")).Id.Should().Be(own.Id, "the character is still there");
    }

    [Fact]
    public async Task Missing_or_revoked_seat_is_404()
    {
        var (seat, own, _) = await Seated("SemConvite");

        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/characters/{own.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/table/seats/{seat.Id}/revoke")).EnsureSuccessStatusCode();
        (await _t.Raw(_t.Player, HttpMethod.Get, $"/api/characters/{own.Id}", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
