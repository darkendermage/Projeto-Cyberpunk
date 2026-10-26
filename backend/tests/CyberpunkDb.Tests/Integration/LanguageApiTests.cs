using System.Net;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Messages follow Accept-Language (or ?lang=): Portuguese for "pt*", English for anything else.</summary>
[Collection(ApiCollection.Name)]
public class LanguageApiTests(ApiFactory factory)
{
    private async Task<(HttpStatusCode Status, string Body)> Get(string url, string? language)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        if (language is not null) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Errors_come_in_the_requested_language()
    {
        var english = await Get("/api/lists/999999", "en-US");
        english.Status.Should().Be(HttpStatusCode.NotFound);
        english.Body.Should().Contain("List 999999 not found.");

        (await Get("/api/lists/999999", "pt-BR")).Body.Should().Contain("Lista 999999 não encontrada.");
        (await Get("/api/lists/999999", null)).Body.Should().Contain("List 999999 not found.");
        (await Get("/api/lists/999999?lang=pt-BR", "en-US")).Body.Should().Contain("Lista 999999 não encontrada.");
        (await Get("/api/does-not-exist", "en-US")).Body.Should().Contain("API route not found.");
    }
}
