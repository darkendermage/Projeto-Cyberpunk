using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// Images of saved NPCs and of the compendium's named NPCs, which the table's tokens inherit
[Collection(ApiCollection.Name)]
public class NpcPortraitsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // A saved NPC built by the generator (the same path the generator screen takes)
    internal static async Task<int> SavedNpc(HttpClient client, string name = "Retratado")
    {
        var generated = (await (await client.PostAsJsonAsync("/api/npcs/generate", new { level = "Mook", seed = 7 })).Content.ReadFromJsonAsync<JsonNode>())!;
        var sheet = generated["sheet"]!.AsObject();
        sheet["name"] = name;
        var create = await client.PostAsJsonAsync("/api/npcs", new JsonObject { ["sheet"] = sheet.DeepClone() });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        return (await create.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<int>();
    }

    internal static async Task<HttpResponseMessage> PutImage(HttpClient client, string url, byte[] bytes, string type = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", "image.png");
        return await client.PutAsync(url, form);
    }

    private async Task<string> FirstCompendiumId() =>
        (await _client.GetFromJsonAsync<JsonArray>("/api/npc-compendium"))![0]!["id"]!.GetValue<string>();

    [Fact]
    public async Task Saved_npc_image_round_trips()
    {
        var id = await SavedNpc(_client);

        (await PutImage(_client, $"/api/npcs/{id}/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await _client.GetAsync($"/api/npcs/{id}/portrait");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await get.Content.ReadAsByteArrayAsync()).Should().Equal(TableFoundationApiTests.Png);
        (await _client.GetFromJsonAsync<JsonObject>("/api/npcs/portraits"))!.ContainsKey(id.ToString()).Should().BeTrue();

        (await _client.DeleteAsync($"/api/npcs/{id}/portrait")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/npcs/{id}/portrait")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Compendium_image_round_trips()
    {
        var id = await FirstCompendiumId();
        var url = $"/api/npc-compendium/{Uri.EscapeDataString(id)}/portrait";

        (await PutImage(_client, url, TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetByteArrayAsync(url)).Should().Equal(TableFoundationApiTests.Png);
        (await _client.GetFromJsonAsync<JsonObject>("/api/npc-compendium/portraits"))!.ContainsKey(id).Should().BeTrue();

        (await _client.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetFromJsonAsync<JsonObject>("/api/npc-compendium/portraits"))!.ContainsKey(id).Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_compendium_id_is_404()
    {
        (await PutImage(_client, "/api/npc-compendium/nao-existe/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_saved_npc_is_404()
    {
        (await PutImage(_client, "/api/npcs/999999/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_copies_the_image()
    {
        var id = await SavedNpc(_client);
        await PutImage(_client, $"/api/npcs/{id}/portrait", TableFoundationApiTests.Png);

        var copy = (await (await _client.PostAsync($"/api/npcs/{id}/duplicate", null)).Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<int>();

        (await _client.GetByteArrayAsync($"/api/npcs/{copy}/portrait")).Should().Equal(TableFoundationApiTests.Png);
    }

    [Fact]
    public async Task Deleting_the_npc_drops_its_image()
    {
        var id = await SavedNpc(_client);
        await PutImage(_client, $"/api/npcs/{id}/portrait", TableFoundationApiTests.Png);

        (await _client.DeleteAsync($"/api/npcs/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.GetFromJsonAsync<JsonObject>("/api/npcs/portraits"))!.ContainsKey(id.ToString()).Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_non_images()
    {
        var id = await SavedNpc(_client);
        (await PutImage(_client, $"/api/npcs/{id}/portrait", "hello"u8.ToArray(), "text/plain")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
