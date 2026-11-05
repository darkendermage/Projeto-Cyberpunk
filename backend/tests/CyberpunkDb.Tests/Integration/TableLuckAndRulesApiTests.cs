using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Application.Table.Dice;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>LUCK spent on a Check (+1 a point, CP:R p.130), the table's 1-LUCK reroll, and the rules a player can look up.</summary>
[Collection(ApiCollection.Name)]
public class TableLuckAndRulesApiTests(ApiFactory factory)
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
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<(SeatDto Seat, int CharacterId)> LuckySeat(string name)
    {
        var c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            expected: HttpStatusCode.Created);
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with
        {
            Creation = c.Sheet.Creation with { StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 } },
        }, null));
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Dona de {name}", c.Id), expected: HttpStatusCode.Created);
        return (seat, c.Id);
    }

    private async Task<int> LuckLeft(HttpClient client, string seat) =>
        (await Send<PlayerCharacterDto>(client, HttpMethod.Get, "/api/table/play/character", seat: seat)).LuckAvailable;

    [Fact]
    public async Task Luck_spent_on_a_check_adds_one_a_point_and_leaves_the_pool()
    {
        var (seat, _) = await LuckySeat("Sortuda");
        var before = await LuckLeft(_player, seat.Token);

        var m = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+5", RollKind.Check, "Atletismo", Luck: 2), seat.Token);

        m.Roll!.Luck.Should().Be(2);
        var dice = m.Roll.Terms.Sum(t => t.Value);
        m.Roll.Total.Should().Be(dice + 2);
        (await LuckLeft(_player, seat.Token)).Should().Be(before - 2);
    }

    [Fact]
    public async Task Luck_is_refused_when_the_pool_runs_short_or_the_roll_is_not_a_check()
    {
        var (seat, _) = await LuckySeat("Azarada");
        var before = await LuckLeft(_player, seat.Token);

        await Send<JsonElement>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+5", RollKind.Check, Luck: before + 1), seat.Token, HttpStatusCode.BadRequest);
        await Send<JsonElement>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("3d6", RollKind.Damage, Luck: 1), seat.Token, HttpStatusCode.BadRequest);
        await Send<JsonElement>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check, Luck: 1), expected: HttpStatusCode.BadRequest);

        (await LuckLeft(_player, seat.Token)).Should().Be(before, "nada é gasto quando a rolagem é recusada");
    }

    [Fact]
    public async Task A_reroll_costs_one_luck_and_happens_once()
    {
        var (seat, _) = await LuckySeat("Rerrola");
        var other = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Outra"), expected: HttpStatusCode.Created);
        var first = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+7", RollKind.Check, "Pistola", Luck: 1), seat.Token);
        var before = await LuckLeft(_player, seat.Token);

        var again = await Send<TableMessageDto>(_player, HttpMethod.Post, $"/api/table/play/rolls/{first.Id}/reroll", seat: seat.Token);

        again.Roll!.RerollOf.Should().Be(first.Id);
        again.Roll.Luck.Should().Be(1, "a Sorte gasta antes continua valendo");
        again.Roll.Total.Should().Be(again.Roll.Terms.Sum(t => t.Value) + 1);
        (await LuckLeft(_player, seat.Token)).Should().Be(before - 1);
        await Send<JsonElement>(_player, HttpMethod.Post, $"/api/table/play/rolls/{first.Id}/reroll", seat: seat.Token, expected: HttpStatusCode.BadRequest);
        await Send<JsonElement>(_player, HttpMethod.Post, $"/api/table/play/rolls/{again.Id}/reroll", seat: other.Token, expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Players_look_up_the_books_tables_through_the_table()
    {
        var (seat, _) = await LuckySeat("Leitora");

        var rules = await Send<TableRulesDto>(_player, HttpMethod.Get, "/api/table/play/rules", seat: seat.Token);

        rules.CriticalInjuries.Should().HaveCount(22);
        rules.CoverMaterials.Should().Contain(m => m.Id == "steel" && m.Thick == 50);
        rules.CoverExamples.Should().NotBeEmpty();
        rules.Notes.Should().OnlyContain(n => n.Source.Contains("CP:R"));
        (await Send<TableRulesDto>(_gm, HttpMethod.Get, "/api/table/rules")).CriticalInjuries.Should().HaveCount(22);
    }
}
