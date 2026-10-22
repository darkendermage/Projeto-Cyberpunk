using System.Net;
using System.Security.Cryptography;
using System.Text;
using CyberpunkDb.Api.Desktop;
using CyberpunkDb.Application.Common;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Desktop;

public sealed class AppUpdaterTests : IDisposable
{
    private const string Repo = "dono/repo";
    private const string ExeUrl = "https://example.test/NightCityItemDB.exe";
    private const string HashUrl = "https://example.test/NightCityItemDB.exe.sha256";
    private static readonly byte[] NewExe = Encoding.UTF8.GetBytes("versao nova");

    private readonly string _dir = Directory.CreateTempSubdirectory("ncdb-updater-").FullName;
    private readonly FakeHandler _http = new();

    private string Exe => Path.Combine(_dir, "NightCityItemDB.exe");

    public AppUpdaterTests()
    {
        File.WriteAllText(Exe, "versao antiga");
        _http.Respond($"https://api.github.com/repos/{Repo}/releases/latest", Release("v1.2.0"));
        _http.Respond(ExeUrl, NewExe);
        _http.Respond(HashUrl, $"{Sha256(NewExe)}  NightCityItemDB.exe\n");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("v1.3.0", "1.3.0")]
    [InlineData("1.3.0", "1.3.0")]
    [InlineData("1.3.0-dev", "1.3.0")]
    [InlineData("0.0.0-dev+abc123", "0.0.0")]
    public void Parses_tags_and_build_versions(string value, string expected) =>
        AppVersion.Parse(value).Should().Be(Version.Parse(expected));

    [Fact]
    public void Rejects_tags_that_are_not_versions() => AppVersion.Parse("latest").Should().BeNull();

    [Fact]
    public async Task Offers_a_newer_release()
    {
        using var updater = Updater("1.1.0");

        var info = await updater.CheckAsync(default);

        info.Should().BeEquivalentTo(new UpdateInfo("1.1.0", "1.2.0", true, "Notas", "https://github.com/dono/repo/releases/tag/v1.2.0", true));
    }

    [Fact]
    public async Task Same_or_older_release_is_not_offered()
    {
        using var updater = Updater("1.2.0");
        (await updater.CheckAsync(default)).Available.Should().BeFalse();
    }

    [Fact]
    public async Task Checks_github_once_and_reuses_the_answer()
    {
        using var updater = Updater("1.1.0");
        await updater.CheckAsync(default);
        await updater.CheckAsync(default);
        _http.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Outside_the_executable_nothing_is_requested()
    {
        using var updater = new AppUpdater(new AppUpdaterOptions(false, Repo, "1.1.0", Exe), _http);

        var info = await updater.CheckAsync(default);

        info.Available.Should().BeFalse();
        info.CanApply.Should().BeFalse();
        _http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Offline_is_not_an_error()
    {
        _http.Fail = true;
        using var updater = Updater("1.1.0");
        (await updater.CheckAsync(default)).Available.Should().BeFalse();
    }

    [Fact]
    public async Task Release_without_the_executable_is_ignored()
    {
        _http.Respond($"https://api.github.com/repos/{Repo}/releases/latest", Release("v1.2.0", withAssets: false));
        using var updater = Updater("1.1.0");
        (await updater.CheckAsync(default)).Available.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_replaces_the_executable_and_keeps_the_old_one_aside()
    {
        using var updater = Updater("1.1.0");

        await updater.ApplyAsync("1.2.0", default);

        File.ReadAllBytes(Exe).Should().Equal(NewExe);
        File.ReadAllText(Exe + ".old").Should().Be("versao antiga");
        File.Exists(Exe + ".new").Should().BeFalse();

        AppUpdater.CleanupOldBinaries(Exe);
        File.Exists(Exe + ".old").Should().BeFalse();
    }

    [Fact]
    public async Task Apply_refuses_a_download_with_the_wrong_hash()
    {
        _http.Respond(HashUrl, new string('0', 64));
        using var updater = Updater("1.1.0");

        var apply = () => updater.ApplyAsync("1.2.0", default);

        await apply.Should().ThrowAsync<ConflictException>().WithMessage("*SHA-256*");
        File.ReadAllText(Exe).Should().Be("versao antiga");
        File.Exists(Exe + ".new").Should().BeFalse();
    }

    [Fact]
    public async Task Apply_refuses_a_version_that_is_not_the_latest()
    {
        using var updater = Updater("1.1.0");
        var apply = () => updater.ApplyAsync("1.5.0", default);
        await apply.Should().ThrowAsync<ConflictException>();
        File.ReadAllText(Exe).Should().Be("versao antiga");
    }

    [Fact]
    public void Swap_restores_the_original_when_the_new_file_cannot_be_moved()
    {
        var act = () => AppUpdater.Swap(Exe, Path.Combine(_dir, "nao-existe.new"));

        act.Should().Throw<FileNotFoundException>();
        File.ReadAllText(Exe).Should().Be("versao antiga");
        File.Exists(Exe + ".old").Should().BeFalse();
    }

    private AppUpdater Updater(string current) => new(new AppUpdaterOptions(true, Repo, current, Exe), _http);

    private static string Release(string tag, bool withAssets = true)
    {
        var assets = withAssets
            ? $$"""{ "name": "NightCityItemDB.exe", "browser_download_url": "{{ExeUrl}}" }, { "name": "NightCityItemDB.exe.sha256", "browser_download_url": "{{HashUrl}}" }"""
            : "";
        return $$"""
            {
              "tag_name": "{{tag}}",
              "body": "Notas",
              "html_url": "https://github.com/dono/repo/releases/tag/{{tag}}",
              "assets": [{{assets}}]
            }
            """;
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = new();

        public List<string> Requests { get; } = [];
        public bool Fail { get; set; }

        public void Respond(string url, string body) => _responses[url] = Encoding.UTF8.GetBytes(body);
        public void Respond(string url, byte[] body) => _responses[url] = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            if (Fail) throw new HttpRequestException("offline");
            return Task.FromResult(_responses.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
