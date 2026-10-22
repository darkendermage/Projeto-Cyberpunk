using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AppUpdateApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_the_running_version()
    {
        var health = (await _client.GetFromJsonAsync<JsonNode>("/api/health"))!;

        health["status"]!.GetValue<string>().Should().Be("online");
        health["version"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Outside_the_executable_no_update_is_offered()
    {
        var info = (await _client.GetFromJsonAsync<JsonNode>("/api/app/update"))!;

        info["available"]!.GetValue<bool>().Should().BeFalse();
        info["canApply"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Applying_outside_the_executable_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/app/update", new { version = "9.9.9" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_requires_a_json_body_so_other_sites_cannot_trigger_it()
    {
        // A cross-site request without preflight can only use "simple" content types such as text/plain.
        var response = await _client.PostAsync("/api/app/update", new StringContent("""{ "version": "9.9.9" }""", Encoding.UTF8, "text/plain"));
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
