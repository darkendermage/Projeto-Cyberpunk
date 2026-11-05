using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Several tokens and areas moved, cleared or hidden at once (a lasso selection), in one save.</summary>
[Collection(ApiCollection.Name)]
public class TableBatchApiTests(ApiFactory factory)
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

    private Task<SceneView> Gm(HttpMethod method, string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK) =>
        Send<SceneView>(_gm, method, url, body, expected: expected);

    [Fact]
    public async Task The_gm_moves_clears_and_hides_a_selection_at_once()
    {
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Lote do mestre"), HttpStatusCode.Created);
        foreach (var label in new[] { "A", "B", "C" })
            scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput(label, 1, 1));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/templates", new TemplateInput(TemplateShape.Square, 2, 2, 10, Label: "Área"));
        string T(string label) => scene.Tokens.Single(t => t.Label == label).Id;
        var area = scene.Templates.Single().Id;

        var after = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/batch", new SceneBatchRequest(
            MoveTokens: [new BatchMove(T("A"), 5, 6), new BatchMove(T("B"), 7, 8)],
            MoveTemplates: [new BatchMove(area, 9, 9)],
            RemoveTokens: [T("C")],
            HideTokens: [T("B")]));

        after.Tokens.Should().HaveCount(2);
        after.Tokens.Single(t => t.Label == "A").Should().Match<TokenView>(t => t.X == 5 && t.Y == 6 && t.Visibility == TokenVisibility.All);
        after.Tokens.Single(t => t.Label == "B").Should().Match<TokenView>(t => t.X == 7 && t.Visibility == TokenVisibility.Gm);
        after.Templates.Single().Should().Match<SceneTemplate>(t => t.X == 9 && t.Y == 9);

        after = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/batch", new SceneBatchRequest(ShowTokens: [T("B")], RemoveTemplates: [area]));
        after.Tokens.Single(t => t.Label == "B").Visibility.Should().Be(TokenVisibility.All);
        after.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task A_players_batch_takes_only_what_they_may_touch()
    {
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Do lote"), expected: HttpStatusCode.Created);
        var other = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Outro do lote"), expected: HttpStatusCode.Created);
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Lote do jogador"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Meu", 1, 1, OwnerSeatId: seat.Id));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Do mestre", 3, 3));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/templates", new TemplateInput(TemplateShape.Square, 2, 2, 10, Label: "Do mestre"));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        var withArea = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/templates", new TemplateInput(TemplateShape.Square, 6, 6, 10, Label: "Do outro"), other.Token);
        var mine = scene.Tokens.Single(t => t.Label == "Meu").Id;
        var gmToken = scene.Tokens.Single(t => t.Label == "Do mestre").Id;
        var gmArea = scene.Templates.Single().Id;
        var otherArea = withArea.Templates.Single(t => t.Label == "Do outro").Id;

        var ok = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/batch",
            new SceneBatchRequest(MoveTokens: [new BatchMove(mine, 4, 4)], MoveTemplates: [new BatchMove(otherArea, 7, 7)]), seat.Token);
        ok.Tokens.Single(t => t.Id == mine).X.Should().Be(4);
        ok.Templates.Single(t => t.Id == otherArea).X.Should().Be(7);

        await Send<JsonElement>(_player, HttpMethod.Post, "/api/table/play/batch",
            new SceneBatchRequest(MoveTokens: [new BatchMove(mine, 9, 9), new BatchMove(gmToken, 9, 9)]), seat.Token, HttpStatusCode.BadRequest);
        await Send<JsonElement>(_player, HttpMethod.Post, "/api/table/play/batch",
            new SceneBatchRequest(RemoveTemplates: [gmArea]), seat.Token, HttpStatusCode.BadRequest);
        await Send<JsonElement>(_player, HttpMethod.Post, "/api/table/play/batch",
            new SceneBatchRequest(RemoveTokens: [mine]), seat.Token, HttpStatusCode.BadRequest);

        var state = await Send<SceneView>(_gm, HttpMethod.Get, $"/api/table/scenes/{scene.Id}");
        state.Tokens.Single(t => t.Id == mine).X.Should().Be(4, "um lote recusado não muda nada");
        state.Templates.Should().Contain(t => t.Id == gmArea);

        var cleared = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/batch", new SceneBatchRequest(RemoveTemplates: [otherArea]), seat.Token);
        cleared.Templates.Should().NotContain(t => t.Id == otherArea);
    }
}
