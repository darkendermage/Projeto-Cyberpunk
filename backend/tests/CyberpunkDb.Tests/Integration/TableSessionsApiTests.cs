using System.Net;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>
/// Sessions of the table (E2): "New session" archives the chat and the fights under a name; the GM reads old sessions;
/// players see only the open one. The suite shares one database, so each test reads relative to what it created.
/// </summary>
[Collection(ApiCollection.Name)]
public class TableSessionsApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private Task<List<TableSessionDto>> Sessions() => _t.Send<List<TableSessionDto>>(_t.Gm, HttpMethod.Get, "/api/table/sessions");

    private Task<TableMessageDto> Say(string text) =>
        _t.Send<TableMessageDto>(_t.Gm, HttpMethod.Post, "/api/table/messages", new SendMessageRequest(text, TableVisibility.Public, null));

    [Fact]
    public async Task New_session_starts_empty_chat_and_archives_old()
    {
        await Say("antes da nova sessão");
        var before = (await Sessions()).Single(s => s.Open);

        var opened = await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest("Sessão do teste"));

        opened.Open.Should().BeTrue();
        opened.Name.Should().Be("Sessão do teste", "the name typed is the new session's (round 6)");
        (await _t.Send<List<TableMessageDto>>(_t.Gm, HttpMethod.Get, "/api/table/messages")).Should().BeEmpty();
        var archived = (await Sessions()).Single(s => s.Id == before.Id);
        archived.Open.Should().BeFalse();
        archived.Name.Should().Be(before.Name, "the session that ends keeps its name");
        archived.Messages.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Session_numbers_never_repeat_after_a_delete()
    {
        var first = await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        (await _t.Raw(_t.Gm, HttpMethod.Delete, $"/api/table/sessions/{first.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));

        var names = (await Sessions()).Select(s => s.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Messages_go_to_open_session_and_archives_stay_readable()
    {
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        await Say("guardada");
        var open = (await Sessions()).Single(s => s.Open);
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));

        var archived = await _t.Send<List<TableMessageDto>>(_t.Gm, HttpMethod.Get, $"/api/table/sessions/{open.Id}/messages");

        archived.Should().Contain(m => m.Text == "guardada");
    }

    [Fact]
    public async Task List_shows_fights_of_the_session()
    {
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        var s = await _t.FightOnTheTable("FightNaSessao");

        var open = (await Sessions()).Single(x => x.Open);

        open.Fights.Should().Contain(f => f.Id == s.EncounterId);
    }

    [Fact]
    public async Task Cannot_delete_open_session_but_can_rename_and_delete_an_old_one()
    {
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        var old = (await Sessions()).Single(s => s.Open);
        (await _t.Raw(_t.Gm, HttpMethod.Delete, $"/api/table/sessions/{old.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        var renamed = await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Put, $"/api/table/sessions/{old.Id}", new RenameSessionRequest("Novo nome"));
        renamed.Name.Should().Be("Novo nome");
        (await _t.Raw(_t.Gm, HttpMethod.Delete, $"/api/table/sessions/{old.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Sessions()).Should().NotContain(s => s.Id == old.Id);
    }

    [Fact]
    public async Task Player_sees_only_open_session()
    {
        var solo = await _t.FinalizedSolo("LeitorDeSessao");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Leitor", solo.Id), expected: HttpStatusCode.Created);
        await Say("velha");
        await _t.Send<TableSessionDto>(_t.Gm, HttpMethod.Post, "/api/table/sessions", new NewSessionRequest(null));
        await Say("nova");

        var messages = await _t.Send<List<TableMessageDto>>(_t.Player, HttpMethod.Get, "/api/table/play/messages", seat: seat.Token);

        messages.Should().Contain(m => m.Text == "nova").And.NotContain(m => m.Text == "velha");
        (await _t.Raw(_t.Player, HttpMethod.Get, "/api/table/sessions", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
