using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class NpcCompendiumApiTests(ApiFactory factory)
{
    private async Task<(HttpStatusCode Status, string Body)> Get(string url, string language = "en-US")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_has_sheets_and_profiles()
    {
        var (status, body) = await Get("/api/npc-compendium");
        status.Should().Be(HttpStatusCode.OK);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();

        string Kind(string id) => items.Single(i => i.GetProperty("id").GetString() == id).GetProperty("kind").GetString()!;
        Kind("adam-smasher").Should().Be("Sheet");
        Kind("morgan-blackhand").Should().Be("Profile");

        var names = items.Select(i => i.GetProperty("name").GetString()!).ToList();
        names.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detail_returns_the_printed_sheet()
    {
        var (status, body) = await Get("/api/npc-compendium/adam-smasher");
        status.Should().Be(HttpStatusCode.OK);
        var sheet = JsonDocument.Parse(body).RootElement.GetProperty("sheet");
        sheet.GetProperty("hp").GetInt32().Should().Be(75);
        sheet.GetProperty("deathSave").GetInt32().Should().Be(17);
        sheet.GetProperty("stats").GetProperty("BODY").GetInt32().Should().Be(17);
        sheet.GetProperty("stats").GetProperty("EMP").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Texts_follow_the_language()
    {
        string Summary(string body) => JsonDocument.Parse(body).RootElement.GetProperty("summary").GetString()!;
        var en = Summary((await Get("/api/npc-compendium/adam-smasher", "en-US")).Body);
        var pt = Summary((await Get("/api/npc-compendium/adam-smasher", "pt-BR")).Body);
        en.Should().NotBeEmpty();
        en.Should().NotBe(pt);
    }

    [Fact]
    public async Task Unknown_id_is_404()
    {
        var (status, body) = await Get("/api/npc-compendium/nobody");
        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("isn't in the compendium");
    }
}
