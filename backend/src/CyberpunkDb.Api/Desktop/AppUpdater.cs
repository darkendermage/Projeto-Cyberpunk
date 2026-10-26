using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Api.Desktop;

/// <summary>Version of the running build (MSBuild property Version, passed by publicar.ps1 / the release workflow).</summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Repository embedded by the build (MSBuild property UpdatesRepository); null in local builds.</summary>
    public static string? Repository { get; } = typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "UpdatesRepository")?.Value;

    /// <summary>"v1.3.0", "1.3.0" and "1.3.0-dev" all become 1.3.0; anything else is null.</summary>
    public static Version? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var core = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(core, out var version) ? version : null;
    }
}

public sealed record AppUpdaterOptions(bool Enabled, string? Repository, string CurrentVersion, string? ExePath)
{
    public const string ExeAsset = "NightCityItemDB.exe";
    public const string HashAsset = ExeAsset + ".sha256";

    /// <summary>Self-update needs the desktop executable and a GitHub repository ("owner/name") to read releases from.</summary>
    public bool CanApply => Enabled && !string.IsNullOrWhiteSpace(Repository) && ExePath is not null;

    public static AppUpdaterOptions FromConfiguration(IConfiguration configuration) => new(
        configuration.GetValue<bool>("Desktop:Enabled") && configuration.GetValue<bool>("Updates:Enabled"),
        configuration["Updates:Repository"] is { Length: > 0 } repository ? repository : AppVersion.Repository,
        AppVersion.Current,
        Environment.ProcessPath);
}

public sealed record UpdateInfo(string Current, string? Latest, bool Available, string? Notes, string? ReleaseUrl, bool CanApply);

public sealed record ApplyUpdateRequest(string Version);

/// <summary>
/// Checks the latest GitHub release and replaces the running executable with it.
/// Windows lets a running .exe be renamed (not overwritten), so the swap is: download to .new,
/// verify SHA-256, rename the current file to .old, rename .new into place, restart.
/// </summary>
public sealed class AppUpdater : IDisposable
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetryFailedCheckAfter = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppUpdaterOptions _options;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private Release? _latest;
    private DateTime _checkedAt = DateTime.MinValue;
    private bool _lastCheckFailed;

    public AppUpdater(AppUpdaterOptions options, HttpMessageHandler? handler = null)
    {
        _options = options;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromMinutes(10); // the exe is ~50 MB
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NightCityItemDB", options.CurrentVersion));
    }

    public AppUpdaterOptions Options => _options;

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct)
    {
        var release = await GetLatestAsync(ct);
        return new UpdateInfo(
            _options.CurrentVersion,
            release?.Version.ToString(),
            release is not null && IsNewer(release),
            release?.Notes,
            release?.Url,
            _options.CanApply);
    }

    /// <summary>Downloads and installs <paramref name="version"/>; the caller restarts the application afterwards.</summary>
    public async Task ApplyAsync(string version, CancellationToken ct)
    {
        if (!_options.CanApply)
            throw new ConflictException(L.T("A atualização automática só funciona no executável para Windows.", "Automatic updates only work in the Windows executable."));
        if (!await _applyLock.WaitAsync(0, ct))
            throw new ConflictException(L.T("Uma atualização já está em andamento.", "An update is already in progress."));

        try
        {
            var release = await GetLatestAsync(ct);
            if (release is null || !IsNewer(release) || release.Version != AppVersion.Parse(version))
                throw new ConflictException(L.T("Essa versão não está mais disponível. Recarregue a página.", "That version is no longer available. Reload the page."));

            var exe = _options.ExePath!;
            var downloaded = exe + ".new";
            var expected = await DownloadHashAsync(release.HashUrl, ct);
            var actual = await DownloadAsync(release.ExeUrl, downloaded, ct);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(downloaded);
                throw new ConflictException(L.T("O arquivo baixado está corrompido (SHA-256 não confere). Tente de novo.", "The downloaded file is corrupted (SHA-256 doesn't match). Try again."));
            }

            Swap(exe, downloaded);
        }
        finally
        {
            _applyLock.Release();
        }
    }

    /// <summary>Removes the files a previous update left next to the executable.</summary>
    public static void CleanupOldBinaries(string? exePath)
    {
        if (exePath is null) return;
        TryDelete(exePath + ".old");
        TryDelete(exePath + ".new");
    }

    /// <summary>Puts <paramref name="downloaded"/> in place of <paramref name="exe"/>, restoring the original on failure.</summary>
    public static void Swap(string exe, string downloaded)
    {
        var old = exe + ".old";
        File.Move(exe, old, overwrite: true);
        try
        {
            File.Move(downloaded, exe);
        }
        catch (Exception)
        {
            File.Move(old, exe, overwrite: true);
            TryDelete(downloaded);
            throw;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _checkLock.Dispose();
        _applyLock.Dispose();
    }

    private bool IsNewer(Release release) =>
        AppVersion.Parse(_options.CurrentVersion) is not { } current || release.Version > current;

    private async Task<Release?> GetLatestAsync(CancellationToken ct)
    {
        if (!_options.CanApply) return null;

        await _checkLock.WaitAsync(ct);
        try
        {
            var age = DateTime.UtcNow - _checkedAt;
            if (age < (_lastCheckFailed ? RetryFailedCheckAfter : CacheFor)) return _latest;

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_options.Repository}/releases/latest");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                using var response = await _http.SendAsync(request, timeout.Token);
                response.EnsureSuccessStatusCode();
                var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(Json, timeout.Token);
                _latest = release is null ? null : Release.From(release);
                _lastCheckFailed = false;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                _lastCheckFailed = true; // offline or GitHub unavailable: the app just keeps the current version
            }

            _checkedAt = DateTime.UtcNow;
            return _latest;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<string> DownloadHashAsync(string url, CancellationToken ct)
    {
        try
        {
            // sha256sum format ("<hash>  NightCityItemDB.exe") or just the hash.
            var text = await _http.GetStringAsync(url, ct);
            return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        }
        catch (HttpRequestException ex)
        {
            throw new ConflictException(L.T($"Não foi possível baixar a atualização: {ex.Message}", $"Couldn't download the update: {ex.Message}"));
        }
    }

    /// <summary>Streams the file to disk and returns its SHA-256.</summary>
    private async Task<string> DownloadAsync(string url, string path, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    sha.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }
        catch (UnauthorizedAccessException)
        {
            throw new ConflictException(
                L.T($"Sem permissão para gravar em {Path.GetDirectoryName(path)}. Mova o NightCityItemDB.exe para uma pasta sua ", $"No permission to write to {Path.GetDirectoryName(path)}. Move NightCityItemDB.exe to a folder of yours ") +
                L.T("(por exemplo, Documentos) e tente de novo.", "(for example, Documents) and try again."));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            TryDelete(path);
            throw new ConflictException(L.T($"Não foi possível baixar a atualização: {ex.Message}", $"Couldn't download the update: {ex.Message}"));
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* next start retries */ }
    }

    private sealed record Release(Version Version, string? Notes, string Url, string ExeUrl, string HashUrl)
    {
        public static Release? From(GitHubRelease release)
        {
            var version = AppVersion.Parse(release.TagName);
            var assets = release.Assets ?? [];
            var exe = assets.FirstOrDefault(a => a.Name.Equals(AppUpdaterOptions.ExeAsset, StringComparison.OrdinalIgnoreCase));
            var hash = assets.FirstOrDefault(a => a.Name.Equals(AppUpdaterOptions.HashAsset, StringComparison.OrdinalIgnoreCase));
            if (version is null || exe is null || hash is null) return null;
            return new Release(version, release.Body, release.HtmlUrl, exe.BrowserDownloadUrl, hash.BrowserDownloadUrl);
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        string? Body,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        List<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
