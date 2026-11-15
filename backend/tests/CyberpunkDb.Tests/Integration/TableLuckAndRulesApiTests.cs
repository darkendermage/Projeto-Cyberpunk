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

// LUCK spent on a Check (+1 a point, CP:R p.130), the table's 1-LUCK reroll, and the rules a player can look up.
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
    public async Task Luck_reserved_before_the_check_pays_for_one_free_reroll()
    {
        var (seat, _) = await LuckySeat("Rerrola");
        var other = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Outra"), expected: HttpStatusCode.Created);
        var start = await LuckLeft(_player, seat.Token);
        var plain = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+7", RollKind.Check, "Pistola"), seat.Token);
        // nothing reserved, no reroll
        await Send<JsonElement>(_player, HttpMethod.Post, $"/api/table/play/rolls/{plain.Id}/reroll", seat: seat.Token, expected: HttpStatusCode.BadRequest);

        var first = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+7", RollKind.Check, "Pistola", Luck: 1, RerollLuck: true), seat.Token);
        first.Roll!.RerollReady.Should().BeTrue();
        first.Roll.Total.Should().Be(first.Roll.Terms.Sum(t => t.Value) + 1, "o ponto reservado não soma no teste");
        var before = await LuckLeft(_player, seat.Token);
        before.Should().Be(start - 2);

        var again = await Send<TableMessageDto>(_player, HttpMethod.Post, $"/api/table/play/rolls/{first.Id}/reroll", seat: seat.Token);

        again.Roll!.RerollOf.Should().Be(first.Id);
        again.Roll.RerollReady.Should().BeFalse();
        again.Roll.Luck.Should().Be(1, "a Sorte gasta antes continua valendo");
        again.Roll.Total.Should().Be(again.Roll.Terms.Sum(t => t.Value) + 1);
        (await LuckLeft(_player, seat.Token)).Should().Be(before, "o rerrolar já foi pago");
        await Send<JsonElement>(_player, HttpMethod.Post, $"/api/table/play/rolls/{first.Id}/reroll", seat: seat.Token, expected: HttpStatusCode.BadRequest);
        await Send<JsonElement>(_player, HttpMethod.Post, $"/api/table/play/rolls/{again.Id}/reroll", seat: other.Token, expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_player_reads_their_whole_sheet_through_the_table()
    {
        var (seat, id) = await LuckySeat("Ficha rapida");

        var sheet = await Send<PlayerSheetDto>(_player, HttpMethod.Get, "/api/table/play/sheet", seat: seat.Token);

        sheet.Name.Should().Be("Ficha rapida");
        sheet.Roles.Should().ContainSingle(r => r.Contains("Solo"));
        sheet.Stats.Should().HaveCount(10);
        sheet.MaxHp.Should().BeGreaterThan(0);
        sheet.MaxHumanity.Should().BeGreaterThan(0);
        sheet.Weapons.Should().NotBeEmpty();
        sheet.Cyberware.Should().NotBeEmpty();
        sheet.Gear.Should().NotBeEmpty();
        sheet.Skills.Should().OnlyContain(s => s.Level > 0);
        var loner = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Sem ficha rapida"), expected: HttpStatusCode.Created);
        (await _player.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/table/play/sheet") { Headers = { { "X-Seat", loner.Token } } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        id.Should().BeGreaterThan(0);
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
    [Fact]
    public async Task A_wounded_character_rolls_with_its_debuffs_on_the_line()
    {
        var (seat, id) = await LuckySeat("Ferida");
        var healthy = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+5", RollKind.Check, "Atletismo"), seat.Token);
        healthy.Roll!.Debuffs.Should().BeNull();

        // Seriously Wounded: under half the HP, straight through the armor (CP:R p.220)
        var c = await Send<CharacterDetailDto>(_gm, HttpMethod.Get, $"/api/characters/{id}");
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{id}/combat/damage",
            new DamageRequest(c.Version, c.Report.Health.CurrentHp - c.Report.Health.MaxHp / 2 + 1, BypassArmor: true));
        c.Report.ActionPenalty.Should().BeNegative();

        var typed = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+5", RollKind.Check, "Atletismo"), seat.Token);
        typed.Roll!.Debuffs.Should().Contain(d => d.Kind == "penalty" && d.Value == c.Report.ActionPenalty);
        typed.Roll.PenaltyInTotal.Should().BeFalse();
        var fromSheet = await Send<TableMessageDto>(_player, HttpMethod.Post, "/api/table/play/rolls", new RollRequest("1d10+5", RollKind.Check, "Atletismo", FromSheet: true), seat.Token);
        fromSheet.Roll!.PenaltyInTotal.Should().BeTrue();

        // The GM rolling from that sheet carries them too; a plain GM roll doesn't.
        (await Send<TableMessageDto>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check, CharacterId: id))).Roll!.Debuffs.Should().NotBeNullOrEmpty();
        (await Send<TableMessageDto>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check))).Roll!.Debuffs.Should().BeNull();
    }

    [Fact]
    public async Task The_gm_spends_a_pc_luck_only_when_rolling_from_that_pc_sheet()
    {
        var (seat, id) = await LuckySeat("Pela Ficha");
        var before = await LuckLeft(_player, seat.Token);

        var m = await Send<TableMessageDto>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check, "Atletismo", Luck: 1, CharacterId: id));
        m.Roll!.Luck.Should().Be(1);
        (await LuckLeft(_player, seat.Token)).Should().Be(before - 1);

        await Send<JsonElement>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check, Luck: 1), expected: HttpStatusCode.BadRequest);
        await Send<JsonElement>(_gm, HttpMethod.Post, "/api/table/rolls", new RollRequest("1d10+5", RollKind.Check, Luck: before + 5, CharacterId: id), expected: HttpStatusCode.BadRequest);
    }
}
