using System.Net;
using System.Net.Http.Json;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// The general link: with the entrance open, a player picks a free seat and gets a fresh key for it. The lobby lives in
// memory and is shared by the whole test collection, so every test sets it first and looks only at its own seats.
[Collection(ApiCollection.Name)]
public class TableLobbyApiTests(ApiFactory factory)
{
    private readonly HttpClient _gm = factory.CreateClient();
    private readonly HttpClient _player = factory.CreatePlayerClient();

    private async Task SetOpen(bool open)
    {
        var response = await _gm.PutAsJsonAsync("/api/table/lobby", new SetLobbyRequest(open));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<LobbyStatusDto>())!.Open.Should().Be(open);
    }

    private async Task<SeatDto> NewSeat(string label)
    {
        var response = await _gm.PostAsJsonAsync("/api/table/seats", new CreateSeatRequest(label));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SeatDto>())!;
    }

    private async Task<LobbyDto> Lobby() => (await _player.GetFromJsonAsync<LobbyDto>("/api/table/play/lobby"))!;

    private Task<HttpResponseMessage> Claim(int seatId) => _player.PostAsync($"/api/table/play/lobby/{seatId}/claim", null);

    private Task<HttpResponseMessage> AsSeat(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Seat", token);
        return _player.SendAsync(request);
    }

    [Fact]
    public async Task Closed_lobby_lists_nothing_and_refuses_claims()
    {
        var seat = await NewSeat("Fechada");
        await SetOpen(false);

        var lobby = await Lobby();

        lobby.Open.Should().BeFalse();
        lobby.Seats.Should().BeEmpty();
        (await Claim(seat.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _player.GetAsync($"/api/table/play/lobby/{seat.Id}/portrait")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Open_lobby_lists_free_seats_without_tokens()
    {
        var a = await NewSeat("Livre A");
        var b = await NewSeat("Livre B");
        var revoked = await NewSeat("Revogada");
        await _gm.PostAsync($"/api/table/seats/{revoked.Id}/revoke", null);
        await SetOpen(true);

        var raw = await _player.GetStringAsync("/api/table/play/lobby");

        raw.Should().Contain("Livre A").And.Contain("Livre B").And.NotContain("Revogada");
        raw.Should().NotContain(a.Token).And.NotContain(b.Token).And.NotContain(revoked.Token);
    }

    [Fact]
    public async Task Claim_rotates_the_token_and_hides_the_seat()
    {
        var seat = await NewSeat("Escolhida");
        await SetOpen(true);

        var response = await Claim(seat.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var key = (await response.Content.ReadFromJsonAsync<ClaimResultDto>())!.Token;
        key.Should().NotBe(seat.Token);
        (await AsSeat("/api/table/play/state", key)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AsSeat("/api/table/play/state", seat.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Lobby()).Seats.Should().NotContain(s => s.Id == seat.Id);
        (await _gm.GetFromJsonAsync<List<SeatDto>>("/api/table/seats"))!.Single(s => s.Id == seat.Id).Claimed.Should().BeTrue();
    }

    [Fact]
    public async Task Second_claim_conflicts()
    {
        var seat = await NewSeat("Disputada");
        await SetOpen(true);

        var both = await Task.WhenAll(Claim(seat.Id), Claim(seat.Id));

        both.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
    }

    [Fact]
    public async Task Individual_link_marks_the_seat_taken()
    {
        var seat = await NewSeat("Link individual");
        await SetOpen(true);
        (await Lobby()).Seats.Should().Contain(s => s.Id == seat.Id);

        (await AsSeat("/api/table/play/me", seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Lobby()).Seats.Should().NotContain(s => s.Id == seat.Id);
        (await Claim(seat.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Release_returns_the_seat_and_kills_the_key()
    {
        var seat = await NewSeat("Liberada");
        await SetOpen(true);
        var key = (await (await Claim(seat.Id)).Content.ReadFromJsonAsync<ClaimResultDto>())!.Token;

        var released = await _gm.PostAsync($"/api/table/seats/{seat.Id}/release", null);

        released.StatusCode.Should().Be(HttpStatusCode.OK);
        (await released.Content.ReadFromJsonAsync<SeatDto>())!.Claimed.Should().BeFalse();
        (await AsSeat("/api/table/play/state", key)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Lobby()).Seats.Should().Contain(s => s.Id == seat.Id);
    }

    [Fact]
    public async Task Lobby_controls_are_gm_only()
    {
        (await _player.PutAsJsonAsync("/api/table/lobby", new SetLobbyRequest(true))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _player.GetAsync("/api/table/lobby")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _player.PostAsync("/api/table/seats/1/release", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
