using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Desktop;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TunnelState { Missing, Downloading, Stopped, Starting, Running, Failed }

/// <summary>
/// The tunnel as the GM's panel shows it; <see cref="Url"/> is the public address while it runs, <see cref="LocalUrl"/>
/// the players' port on this machine (for testing a link without the tunnel).
/// </summary>
public sealed record TunnelStatus(TunnelState State, string? Url, string? Error, string? LocalUrl = null);

/// <summary>
/// Puts the players' port on the internet through a Cloudflare Quick Tunnel: no account, no router setup. It runs
/// cloudflared (found in Table:Cloudflared, the tools folder or the PATH, or downloaded from Cloudflare's official
/// GitHub release on the GM's request) and reads the random https://*.trycloudflare.com address it prints. The address
/// changes every time the tunnel opens, and the tunnel closes with the app.
/// </summary>
public sealed partial class TunnelService(IOptions<TableOptions> table, IConfiguration configuration, ILogger<TunnelService> logger) : IDisposable
{
    public const string DownloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(40);

    private readonly Lock _gate = new();
    /// <summary>Ends cloudflared with the app even when no shutdown code runs (window closed, crash).</summary>
    private readonly ChildProcessJob _job = new();
    private Process? _process;
    private TunnelState _state = TunnelState.Stopped;
    private string? _url;
    private string? _error;
    private TaskCompletionSource<string?>? _ready;

    private string? LocalUrl => table.Value.PlayerPort is { } port ? $"http://localhost:{port}" : null;

    public TunnelStatus Status()
    {
        lock (_gate)
        {
            if (_state is TunnelState.Stopped or TunnelState.Failed && Executable() is null)
                return new TunnelStatus(TunnelState.Missing, null, _error, LocalUrl);
            return new TunnelStatus(_state, _url, _error, LocalUrl);
        }
    }

    /// <summary>Opens the tunnel (or returns the one already open) and waits for its public address.</summary>
    public async Task<TunnelStatus> StartAsync(CancellationToken ct)
    {
        if (table.Value.PlayerPort is not { } port)
            throw RequestValidationException.For("tunnel", L.T("A porta dos jogadores (Table:PlayerPort) não está configurada.", "The players' port (Table:PlayerPort) is not configured."));
        var exe = Executable() ?? throw RequestValidationException.For("tunnel", L.T(
            "O cloudflared não está instalado. Baixe-o pelo painel de convites.", "cloudflared is not installed. Download it from the invites panel."));

        Task<string?> ready;
        lock (_gate)
        {
            if (_state is TunnelState.Running or TunnelState.Starting && _ready is not null) ready = _ready.Task;
            else
            {
                _ready = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                ready = _ready.Task;
                _state = TunnelState.Starting;
                _url = null;
                _error = null;
                _process = Launch(exe, port);
            }
        }

        var finished = await Task.WhenAny(ready, Task.Delay(StartTimeout, ct));
        if (finished != ready)
        {
            Fail(L.T("O túnel não respondeu a tempo. Verifique a conexão com a internet e tente de novo.", "The tunnel didn't answer in time. Check the internet connection and try again."));
            Kill();
        }
        return Status();
    }

    public TunnelStatus Stop()
    {
        Kill();
        lock (_gate)
        {
            _state = TunnelState.Stopped;
            _url = null;
            _error = null;
        }
        return Status();
    }

    /// <summary>Downloads cloudflared from Cloudflare's official GitHub release into the tools folder.</summary>
    public async Task<TunnelStatus> DownloadAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw RequestValidationException.For("tunnel", L.T("Instale o cloudflared pelo gerenciador de pacotes do sistema.", "Install cloudflared with the system's package manager."));
        var folder = ToolsDir();
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "cloudflared.exe");
        lock (_gate) { _state = TunnelState.Downloading; _error = null; }
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NightCityItemDB", AppVersion.Current));
            var temp = target + ".download";
            await using (var source = await http.GetStreamAsync(DownloadUrl, ct))
            await using (var file = File.Create(temp))
                await source.CopyToAsync(file, ct);
            // A Windows executable starts with "MZ"; anything else is an error page, not cloudflared.
            await using (var check = File.OpenRead(temp))
            {
                var header = new byte[2];
                if (check.Length < 1024 * 1024 || await check.ReadAsync(header, ct) != 2 || header[0] != 'M' || header[1] != 'Z')
                    throw new InvalidOperationException("Downloaded file is not an executable.");
            }
            File.Move(temp, target, overwrite: true);
            lock (_gate) _state = TunnelState.Stopped;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(e, "cloudflared download failed");
            lock (_gate) _state = TunnelState.Stopped;
            throw RequestValidationException.For("tunnel", L.T("Não deu para baixar o cloudflared. Verifique a internet e tente de novo.", "Couldn't download cloudflared. Check the internet and try again."));
        }
        return Status();
    }

    /// <summary>The trycloudflare address in a line of cloudflared's log, if any.</summary>
    public static string? ParseUrl(string? line)
    {
        if (line is null) return null;
        var m = QuickTunnelUrl().Match(line);
        return m.Success && !m.Value.StartsWith("https://api.", StringComparison.Ordinal) ? m.Value : null;
    }

    public void Dispose()
    {
        Kill();
        _job.Dispose();
    }

    private Process Launch(string exe, int port)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(exe, ["tunnel", "--no-autoupdate", "--url", $"http://localhost:{port}"])
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            },
            EnableRaisingEvents = true,
        };
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);
        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.Exited += (_, _) =>
        {
            lock (_gate)
            {
                if (_process != process) return;
                if (_state is TunnelState.Running or TunnelState.Starting)
                {
                    _state = TunnelState.Failed;
                    _error ??= L.ForSystem("O túnel caiu. Abra de novo.", "The tunnel went down. Open it again.");
                    _url = null;
                }
                _ready?.TrySetResult(null);
            }
        };
        try
        {
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _state = TunnelState.Failed;
            _error = L.T("Não deu para iniciar o cloudflared.", "Couldn't start cloudflared.");
            _ready?.TrySetResult(null);
            return process;
        }
        _job.Add(process);
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        return process;
    }

    private void OnLine(string? line)
    {
        if (ParseUrl(line) is not { } url) return;
        lock (_gate)
        {
            if (_state != TunnelState.Starting) return;
            _url = url;
            _state = TunnelState.Running;
            _ready?.TrySetResult(url);
        }
        logger.LogInformation("Virtual table tunnel open at {Url}", url);
    }

    private void Fail(string error)
    {
        lock (_gate)
        {
            _state = TunnelState.Failed;
            _error = error;
            _url = null;
        }
    }

    private void Kill()
    {
        Process? process;
        lock (_gate)
        {
            process = _process;
            _process = null;
            _ready?.TrySetResult(null);
        }
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { /* already gone */ }
        process.Dispose();
    }

    /// <summary>Table:Cloudflared when set (and present); otherwise the tools folder, then the PATH.</summary>
    private string? Executable()
    {
        var configured = configuration["Table:Cloudflared"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Environment.ExpandEnvironmentVariables(configured);
            return File.Exists(path) ? path : InTools();
        }
        return InTools() ?? OnPath();
    }

    private string? InTools()
    {
        var path = Path.Combine(ToolsDir(), OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared");
        return File.Exists(path) ? path : null;
    }

    private static string? OnPath()
    {
        var name = OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared";
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim('"'), name)).FirstOrDefault(File.Exists);
    }

    private string ToolsDir() => Environment.ExpandEnvironmentVariables(
        configuration["Table:ToolsDir"] is { Length: > 0 } dir ? dir : Path.Combine("%LOCALAPPDATA%", "NightCityItemDB", "tools"));

    [GeneratedRegex(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.IgnoreCase)]
    private static partial Regex QuickTunnelUrl();
}
