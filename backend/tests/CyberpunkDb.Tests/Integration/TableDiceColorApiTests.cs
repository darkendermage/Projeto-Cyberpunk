using System.Net;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// Each person's dice color: stamped on their rolls so every screen throws them in that color
[Collection(ApiCollection.Name)]
public class TableDiceColorApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    [Fact]
    public async Task Roll_message_carries_seat_dice_color()
    {
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Dados"), expected: HttpStatusCode.Created);

        var first = await _t.Send<TableMessageDto>(_t.Player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("2d6"), seat.Token);
        first.Roll!.DiceColor.Should().Be(seat.Color, "without a choice the dice take the seat's color");

        await _t.Send<PlayerSeatDto>(_t.Player, HttpMethod.Put, "/api/table/play/dice-color", new DiceColorRequest("#12ab34"), seat.Token);
        var second = await _t.Send<TableMessageDto>(_t.Player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("2d6"), seat.Token);
        second.Roll!.DiceColor.Should().Be("#12ab34");
    }

    [Fact]
    public async Task Gm_roll_carries_gm_dice_color()
    {
        await _t.Send<DiceColorRequest>(_t.Gm, HttpMethod.Put, "/api/table/dice-color", new DiceColorRequest("#ffffff"));

        var roll = await _t.Send<TableMessageDto>(_t.Gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10"));

        roll.Roll!.DiceColor.Should().Be("#ffffff");
        (await _t.Send<DiceColorRequest>(_t.Gm, HttpMethod.Get, "/api/table/dice-color")).Color.Should().Be("#ffffff");
    }

    [Fact]
    public async Task Invalid_color_is_400()
    {
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Cor ruim"), expected: HttpStatusCode.Created);

        (await _t.Raw(_t.Player, HttpMethod.Put, "/api/table/play/dice-color", new DiceColorRequest("red; drop"), seat.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _t.Raw(_t.Gm, HttpMethod.Put, "/api/table/dice-color", new DiceColorRequest("#12345")).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
