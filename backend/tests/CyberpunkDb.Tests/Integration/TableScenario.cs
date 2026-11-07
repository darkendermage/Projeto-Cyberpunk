using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>
/// The table setups the round-4 tests share: a finalized Solo with a seat, a fight with two gangers linked to a live
/// scene with every token shown.
/// </summary>
internal sealed class TableScenario(ApiFactory factory)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public HttpClient Gm { get; } = factory.CreateClient();
    public HttpClient Player { get; } = factory.CreatePlayerClient();

    public async Task<T> Send<T>(HttpClient client, HttpMethod method, string url, object? body = null, string? seat = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Raw(client, method, url, body, seat);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<HttpResponseMessage> Raw(HttpClient client, HttpMethod method, string url, object? body = null, string? seat = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (seat is not null) request.Headers.Add("X-Seat", seat);
        return await client.SendAsync(request);
    }

    /// <summary>A finalized Streetrat Solo (template row 6), as in EncountersApiTests.</summary>
    public async Task<CharacterDetailDto> FinalizedSolo(string name)
    {
        var c = await Send<CharacterDetailDto>(Gm, HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            expected: HttpStatusCode.Created);
        c = await Send<CharacterDetailDto>(Gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Send<CharacterDetailDto>(Gm, HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with
        {
            Creation = c.Sheet.Creation with { StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 } },
        }, null));
        return await Send<CharacterDetailDto>(Gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
    }

    public sealed record Fight(int EncounterId, int SceneId, SeatDto Seat, int CharacterId, string PcCombatantId, IReadOnlyList<string> GangerIds);

    /// <summary>A Solo and two gangers on the live scene, every token visible to the players.</summary>
    public async Task<Fight> FightOnTheTable(string name, int gangers = 2)
    {
        var solo = await FinalizedSolo(name);
        var seat = await Send<SeatDto>(Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Jogador de {name}", solo.Id), expected: HttpStatusCode.Created);
        var e = await Send<JsonNode>(Gm, HttpMethod.Post, "/api/encounters", new { name = $"Tiroteio de {name}" }, expected: HttpStatusCode.Created);
        var id = e["id"]!.GetValue<int>();
        e = await Send<JsonNode>(Gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Ally", characterId = solo.Id });
        e = await Send<JsonNode>(Gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Enemy", generic = new { name = "Ganger" }, count = gangers });

        var scene = await Send<SceneView>(Gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest($"Rua de {name}"), expected: HttpStatusCode.Created);
        scene = await Send<SceneView>(Gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));
        scene = await Send<SceneView>(Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens");
        await Send<SceneView>(Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/batch", new SceneBatchRequest(ShowTokens: scene.Tokens.Select(t => t.Id).ToList()));
        await Send<SceneView>(Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        var queue = e["queue"]!.AsArray();
        var pc = queue.First(f => f!["characterId"]?.GetValue<int?>() == solo.Id)!["id"]!.GetValue<string>();
        var enemies = queue.Where(f => f!["side"]!.GetValue<string>() == "Enemy").Select(f => f!["id"]!.GetValue<string>()).ToList();
        return new Fight(id, scene.Id, seat, solo.Id, pc, enemies);
    }

    /// <summary>The fight's current version.</summary>
    public async Task<int> VersionOf(int encounterId) =>
        (await Send<JsonNode>(Gm, HttpMethod.Get, $"/api/encounters/{encounterId}"))["version"]!.GetValue<int>();

    /// <summary>Rolls Initiative and makes <paramref name="combatantId"/> the active one.</summary>
    public async Task<JsonNode> MakeActive(int encounterId, string combatantId)
    {
        await Send<JsonNode>(Gm, HttpMethod.Post, $"/api/encounters/{encounterId}/initiative", new { version = await VersionOf(encounterId) });
        return await Send<JsonNode>(Gm, HttpMethod.Post, $"/api/encounters/{encounterId}/combatants/{combatantId}/active", new { version = await VersionOf(encounterId) });
    }
}
