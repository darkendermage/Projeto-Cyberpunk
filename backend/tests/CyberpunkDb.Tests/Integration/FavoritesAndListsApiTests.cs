using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class FavoritesAndListsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Favorites_and_lists_flow()
    {
        var weapon = (await (await _client.PostAsJsonAsync("/api/catalog/weapons/search", new { pageSize = 1 }))
            .Content.ReadFromJsonAsync<JsonNode>())!["items"]![0]!["id"]!.GetValue<int>();

        (await _client.PutAsync($"/api/favorites/{weapon}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetFromJsonAsync<int[]>("/api/favorites/ids")).Should().Contain(weapon);

        var list = (await (await _client.PostAsJsonAsync("/api/lists", new { name = "Loadout de teste" })).Content.ReadFromJsonAsync<JsonNode>())!;
        var listId = list["id"]!.GetValue<int>();
        await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 2 });
        var detail = (await (await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 1 }))
            .Content.ReadFromJsonAsync<JsonNode>())!;
        detail["entries"]!.AsArray().Should().ContainSingle().Which!["quantity"]!.GetValue<int>().Should().Be(3);

        var invalid = await _client.PostAsJsonAsync($"/api/lists/{listId}/entries", new { itemId = weapon, quantity = 0 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.DeleteAsync($"/api/lists/{listId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/favorites/{weapon}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
