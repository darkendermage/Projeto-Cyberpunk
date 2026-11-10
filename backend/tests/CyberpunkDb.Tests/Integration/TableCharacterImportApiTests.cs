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
        // a sheet the GM doesn't have yet
        var file = (await Exported("Importado")) with { Uid = Guid.NewGuid() };
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

    private async Task<int> CharacterCount() => (await _t.Send<List<CharacterSummaryDto>>(_t.Gm, HttpMethod.Get, "/api/characters")).Count;

    [Fact]
    public async Task Reimporting_the_seat_sheet_overwrites_the_GM_copy()
    {
        var own = await _t.FinalizedSolo("Reimporta");
        var file = (await _t.Gm.GetFromJsonAsync<CharacterExportFile>($"/api/characters/{own.Id}/export", TableScenario.Json))!;
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Dono", own.Id), expected: HttpStatusCode.Created);
        var gm = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{own.Id}/balance", new AdjustBalanceRequest(own.Version, 700, "Mesa"));
        var before = await CharacterCount();

        var linked = await _t.Send<PlayerSeatDto>(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file, seat.Token);

        linked.CharacterId.Should().Be(own.Id);
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{own.Id}");
        sheet.Version.Should().BeGreaterThan(gm.Version);
        sheet.Sheet.Wallet.Balance.Should().Be(gm.Sheet.Wallet.Balance - 700, "a ficha do arquivo substitui a do mestre");
        (await CharacterCount()).Should().Be(before);
    }

    [Fact]
    public async Task A_free_sheet_with_the_same_uid_is_updated_and_linked_instead_of_duplicated()
    {
        var file = await Exported("Livre");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Vazio"), expected: HttpStatusCode.Created);
        var before = await CharacterCount();

        var linked = await _t.Send<PlayerSeatDto>(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file, seat.Token);

        var match = await _t.Send<CharacterMatchDto>(_t.Gm, HttpMethod.Get, $"/api/characters/by-uid/{file.Uid}");
        linked.CharacterId.Should().Be(match.Id);
        (await CharacterCount()).Should().Be(before);
    }

    [Fact]
    public async Task A_sheet_played_on_another_seat_cant_be_overwritten()
    {
        var other = await _t.FinalizedSolo("De outro");
        var file = (await _t.Gm.GetFromJsonAsync<CharacterExportFile>($"/api/characters/{other.Id}/export", TableScenario.Json))!;
        await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Outro", other.Id), expected: HttpStatusCode.Created);
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Intruso"), expected: HttpStatusCode.Created);

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file, seat.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{other.Id}")).Version.Should().Be(other.Version);
    }

    [Fact]
    public async Task Import_needs_a_seat()
    {
        var file = await Exported("SemLugar");

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/character/import", file)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
