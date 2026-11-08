using System.Text.Json;
using System.Text.RegularExpressions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table;

// YouTube video and/or playlist, playing or paused at PositionSeconds as of UpdatedAt. Each screen works out
// where the track should be now.
public sealed record TableMusic(string? VideoId, string? PlaylistId, string? Title, bool Playing, double PositionSeconds, DateTime UpdatedAt, int Volume,
    int Index = 0);

public sealed record RecentMusic(string? VideoId, string? PlaylistId, string? Title);

public sealed record TableMusicStateDto(TableMusic? Music, IReadOnlyList<RecentMusic> Recent);

public sealed record LoadMusicRequest(string Url, string? Title);

// null keeps the current position
public sealed record MusicPositionRequest(double? PositionSeconds);

public sealed record MusicVolumeRequest(int Volume);

public sealed record ForgetRecentMusicRequest(string? VideoId, string? PlaylistId);

// watch, youtu.be, music.youtube.com and playlist links. Anything else is refused.
public static partial class YouTubeLink
{
    public static (string? VideoId, string? PlaylistId) Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw Refuse();
        var text = url.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) throw Refuse();
        var host = uri.Host.ToLowerInvariant();
        string? video = null;
        if (host == "youtu.be") video = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")
        {
            if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.Ordinal) || uri.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
                video = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            else video = Query(uri, "v");
        }
        else throw Refuse();

        var list = Query(uri, "list");
        if (video is not null && !VideoId().IsMatch(video)) throw Refuse();
        if (list is not null && !ListId().IsMatch(list)) throw Refuse();
        return video is null && list is null ? throw Refuse() : (video, list);
    }

    private static string? Query(Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key) return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    private static RequestValidationException Refuse() =>
        RequestValidationException.For("url", L.T("Cole um link de vídeo ou playlist do YouTube.", "Paste a YouTube video or playlist link."));

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoId();

    [GeneratedRegex("^[A-Za-z0-9_-]{10,64}$")]
    private static partial Regex ListId();
}

// Stored in the table settings. GM only; players get it with their state and over the hub.
public sealed class TableMusicService(TableSettingsService settings, ITableNotifier notifier)
{
    private const string MusicKey = "music";
    private const string RecentKey = "musicRecent";
    private const int RecentMax = 8;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TableMusic?> CurrentAsync(CancellationToken ct) =>
        await settings.GetAsync(MusicKey, ct) is { } json ? JsonSerializer.Deserialize<TableMusic>(json, Json) : null;

    public async Task<TableMusicStateDto> StateAsync(CancellationToken ct) => new(await CurrentAsync(ct), await RecentAsync(ct));

    // starts paused and goes to the top of the recents
    public async Task<TableMusicStateDto> LoadAsync(LoadMusicRequest r, CancellationToken ct)
    {
        var (video, list) = YouTubeLink.Parse(r.Url);
        var title = string.IsNullOrWhiteSpace(r.Title) ? null : r.Title.Trim()[..Math.Min(80, r.Title.Trim().Length)];
        var volume = (await CurrentAsync(ct))?.Volume ?? 60;
        var recent = (await RecentAsync(ct)).Where(x => x.VideoId != video || x.PlaylistId != list).Prepend(new RecentMusic(video, list, title)).Take(RecentMax).ToList();
        await settings.SetAsync(RecentKey, JsonSerializer.Serialize(recent, Json), ct);
        return await SaveAsync(new TableMusic(video, list, title, false, 0, DateTime.UtcNow, volume), ct);
    }

    public Task<TableMusicStateDto> PlayAsync(MusicPositionRequest r, CancellationToken ct) => ChangeAsync(m => m with { Playing = true, PositionSeconds = r.PositionSeconds ?? Now(m) }, ct);

    public Task<TableMusicStateDto> PauseAsync(CancellationToken ct) => ChangeAsync(m => m with { Playing = false, PositionSeconds = Now(m) }, ct);

    public Task<TableMusicStateDto> SeekAsync(MusicPositionRequest r, CancellationToken ct) =>
        ChangeAsync(m => m with { PositionSeconds = Math.Max(0, r.PositionSeconds ?? 0) }, ct);

    public Task<TableMusicStateDto> NextAsync(CancellationToken ct) =>
        ChangeAsync(m => m.PlaylistId is null ? m with { PositionSeconds = 0 } : m with { Index = m.Index + 1, PositionSeconds = 0, Playing = true }, ct);

    public Task<TableMusicStateDto> VolumeAsync(MusicVolumeRequest r, CancellationToken ct) =>
        ChangeAsync(m => m with { Volume = Math.Clamp(r.Volume, 0, 100) }, ct);

    public async Task<TableMusicStateDto> StopAsync(CancellationToken ct)
    {
        await settings.SetAsync(MusicKey, null, ct);
        await notifier.MusicAsync(null, ct);
        return new TableMusicStateDto(null, await RecentAsync(ct));
    }

    // doesn't stop what's playing
    public async Task<TableMusicStateDto> ForgetRecentAsync(ForgetRecentMusicRequest r, CancellationToken ct)
    {
        var recent = (await RecentAsync(ct)).Where(x => x.VideoId != r.VideoId || x.PlaylistId != r.PlaylistId).ToList();
        await settings.SetAsync(RecentKey, JsonSerializer.Serialize(recent, Json), ct);
        return new TableMusicStateDto(await CurrentAsync(ct), recent);
    }

    // stored position + time since it started playing
    private static double Now(TableMusic m) => m.Playing ? m.PositionSeconds + (DateTime.UtcNow - m.UpdatedAt).TotalSeconds : m.PositionSeconds;

    private async Task<TableMusicStateDto> ChangeAsync(Func<TableMusic, TableMusic> change, CancellationToken ct)
    {
        var current = await CurrentAsync(ct) ?? throw new ConflictException(L.T("Nenhuma música carregada.", "No music loaded."));
        return await SaveAsync(change(current) with { UpdatedAt = DateTime.UtcNow }, ct);
    }

    private async Task<TableMusicStateDto> SaveAsync(TableMusic music, CancellationToken ct)
    {
        await settings.SetAsync(MusicKey, JsonSerializer.Serialize(music, Json), ct);
        await notifier.MusicAsync(music, ct);
        return new TableMusicStateDto(music, await RecentAsync(ct));
    }

    private async Task<IReadOnlyList<RecentMusic>> RecentAsync(CancellationToken ct) =>
        await settings.GetAsync(RecentKey, ct) is { } json ? JsonSerializer.Deserialize<List<RecentMusic>>(json, Json) ?? [] : [];
}
