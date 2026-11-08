using System.Net;
using System.Net.Http.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// A player brings their own sheet: the file exported on their PC, linked to their seat
[Collection(ApiCollection.Name)]
public class TableCharacterImportApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<CharacterExportFile> Exported(string name)
    {
        var c = await _t.FinalizedSolo(name);
        return (await _t.Gm.GetFromJsonAsync<CharacterExportFile>($"/api/characters/{c.Id}/export", TableScenario.Json))!;
    }

    [Fact]
    public async Task Seat_without_character_imports_and_links_it()
    {
        var file = await Exported("Importado");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Sem ficha"), expected: HttpStatusCode.Created);

        var linked = await _t.Send<PlayerSeatDto>(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file, seat.Token, HttpStatusCode.Created);

        linked.CharacterId.Should().NotBeNull();
        var state = await _t.Send<PlayerStateDto>(_t.Player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);
        state.Seat.CharacterId.Should().Be(linked.CharacterId);
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{linked.CharacterId}");
        sheet.Id.Should().Be(linked.CharacterId!.Value);
    }

    [Fact]
    public async Task Seat_with_a_character_cant_swap_it()
    {
        var file = await Exported("Troca");
        var own = await _t.FinalizedSolo("Ja tem");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Com ficha", own.Id), expected: HttpStatusCode.Created);
        var before = (await _t.Send<List<CharacterSummaryDto>>(_t.Gm, HttpMethod.Get, "/api/characters")).Count;

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file, seat.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _t.Send<List<CharacterSummaryDto>>(_t.Gm, HttpMethod.Get, "/api/characters")).Count.Should().Be(before, "nothing is created when the seat already has a sheet");
    }

    [Fact]
    public async Task Import_needs_a_seat()
    {
        var file = await Exported("SemLugar");

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
