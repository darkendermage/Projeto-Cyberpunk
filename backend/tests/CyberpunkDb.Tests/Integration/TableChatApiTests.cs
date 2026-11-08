using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Application.Table.Dice;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace CyberpunkDb.Tests.Integration;

// Chat and dice of the virtual table: the server rolls, and each message reaches only who may read it
[Collection(ApiCollection.Name)]
public class TableChatApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _gm = factory.CreateClient();
    private readonly HttpClient _player = factory.CreatePlayerClient();

    private async Task<T> Send<T>(HttpClient client, HttpMethod method, string url, object? body = null, string? seat = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (seat is not null) request.Headers.Add("X-Seat", seat);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return response.StatusCode == HttpStatusCode.NoContent ? default! : (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private Task<SeatDto> NewSeat(string label) =>
        Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest(label), expected: HttpStatusCode.Created);

    private Task<List<TableMessageDto>> History(string? seat) => seat is null
        ? Send<List<TableMessageDto>>(_gm, HttpMethod.Get, "/api/table/messages")
        : Send<List<TableMessageDto>>(_player, HttpMethod.Get, "/api/table/play/messages", seat: seat);

    [Fact]
    public async Task Public_chat_and_rolls_reach_everyone_live()
    {
        var a = await NewSeat("Alfa");
        var b = await NewSeat("Bravo");
        await using var hubB = TableFoundationApiTests.Hub(factory, b.Token, playerPort: true);
        await using var gm = TableFoundationApiTests.Hub(factory, null, playerPort: false);
        var toB = new TaskCompletionSource<TableMessageDto>();
        var toGm = new TaskCompletionSource<TableMessageDto>();
        hubB.On<TableMessageDto>("message", m => { if (m.Text.Contains("choom-1")) toB.TrySetResult(m); });
        gm.On<TableMessageDto>("message", m => { if (m.Text.Contains("choom-1")) toGm.TrySetResult(m); });
        await hubB.StartAsync();
        await gm.StartAsync();
        await Task.Delay(300);

        var sent = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/messages", new SendMessageRequest("E aí, choom-1"), a.Token);
        sent.AuthorName.Should().Be("Alfa");
        sent.AuthorColor.Should().Be(a.Color);
        (await toB.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id.Should().Be(sent.Id);
        (await toGm.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id.Should().Be(sent.Id);

        var roll = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+12", RollKind.Check, "Handgun"), a.Token);
        roll.Kind.Should().Be(TableMessageKind.Roll);
        roll.Roll!.Total.Should().BeInRange(1 - 10 + 12, 10 + 10 + 12);
        roll.Text.Should().Contain("Handgun");
        (await History(b.Token)).Should().Contain(m => m.Id == roll.Id && m.Roll != null);
    }

    [Fact]
    public async Task Private_secret_and_whispered_lines_reach_only_their_readers()
    {
        var a = await NewSeat("Charlie");
        var b = await NewSeat("Delta");

        var priv = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls",
            new RollRequest("1d10+5", RollKind.Check, "Stealth", TableVisibility.Private), a.Token);
        var secret = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls",
            new RollRequest("1d10+3", RollKind.Check, "Perception", TableVisibility.Secret), a.Token);
        secret.Roll.Should().BeNull("the author of a secret roll doesn't see the result");
        secret.Concealed.Should().BeTrue();
        var whisper = await Send<TableMessageDto>(_gm, HttpMethod.Post, "/api/table/messages",
            new SendMessageRequest("Você ouve passos", TableVisibility.Whisper, a.Id));
        whisper.AuthorName.Should().Be("GM");

        var gmHistory = await History(null);
        gmHistory.Single(m => m.Id == secret.Id).Roll.Should().NotBeNull();
        gmHistory.Should().Contain(m => m.Id == priv.Id).And.Contain(m => m.Id == whisper.Id);

        var aHistory = await History(a.Token);
        aHistory.Single(m => m.Id == priv.Id).Roll.Should().NotBeNull();
        aHistory.Single(m => m.Id == secret.Id).Roll.Should().BeNull();
        aHistory.Should().Contain(m => m.Id == whisper.Id);

        var bHistory = await History(b.Token);
        bHistory.Should().NotContain(m => m.Id == priv.Id || m.Id == secret.Id || m.Id == whisper.Id);
    }

    [Fact]
    public async Task Hidden_lines_are_never_pushed_to_other_players()
    {
        var a = await NewSeat("Echo");
        var b = await NewSeat("Foxtrot");
        await using var hubB = TableFoundationApiTests.Hub(factory, b.Token, playerPort: true);
        await using var hubA = TableFoundationApiTests.Hub(factory, a.Token, playerPort: true);
        var bGot = new List<TableMessageDto>();
        var aGot = new TaskCompletionSource<TableMessageDto>();
        hubB.On<TableMessageDto>("message", m => { lock (bGot) bGot.Add(m); });
        hubA.On<TableMessageDto>("message", m => aGot.TrySetResult(m));
        await hubB.StartAsync();
        await hubA.StartAsync();
        await Task.Delay(300);

        var secret = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls",
            new RollRequest("1d10", RollKind.Check, "Secreto", TableVisibility.Secret), a.Token);
        (await aGot.Task.WaitAsync(TimeSpan.FromSeconds(10))).Roll.Should().BeNull();
        await Task.Delay(300);
        lock (bGot) bGot.Should().NotContain(m => m.Id == secret.Id);
    }

    [Fact]
    public async Task Bad_input_is_refused_and_a_new_session_starts_an_empty_chat()
    {
        var a = await NewSeat("Golf");
        await Send<object>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+abc"), a.Token, HttpStatusCode.BadRequest);
        await Send<object>(_player, HttpMethod.Post, "/api/table/play/messages", new SendMessageRequest("  "), a.Token, HttpStatusCode.BadRequest);
        await Send<object>(_player, HttpMethod.Post, "/api/table/play/messages", new SendMessageRequest("psst", TableVisibility.Whisper, 999999), a.Token, HttpStatusCode.BadRequest);
        await Send<object>(_player, HttpMethod.Post, "/api/table/play/messages", new SendMessageRequest("oi"), null, HttpStatusCode.Unauthorized);

        await Send<TableMessageDto>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("3d6", RollKind.Damage, "Dano"));
        // A new session starts an empty chat (the old one is archived, not deleted).
        await Send<TableSessionDto>(_gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        (await History(null)).Should().BeEmpty();
    }
}
