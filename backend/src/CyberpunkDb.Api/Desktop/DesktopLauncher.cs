using System.Diagnostics;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Api.Desktop;

/// <summary>Desktop executable behaviour: opens the browser and avoids two instances on the same port.</summary>
public static class DesktopLauncher
{
    public const string Title = "Night City Item Database";

    public static string AppUrl(IConfiguration configuration) =>
        (configuration["Urls"] ?? "http://localhost:5090").Split(';')[0]
            .Replace("0.0.0.0", "localhost").Replace("*", "localhost").TrimEnd('/');

    /// <summary>If an instance is already answering, opens the browser on it and returns true.</summary>
    public static async Task<bool> TryReuseRunningInstanceAsync(IConfiguration configuration)
    {
        var url = AppUrl(configuration);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var body = await http.GetStringAsync($"{url}/api/health");
            if (!body.Contains("online", StringComparison.Ordinal)) return false;
        }
        catch (Exception)
        {
            return false; // nothing running: normal startup
        }

        Console.WriteLine(L.ForSystem($"O {Title} já está aberto em {url}.", $"{Title} is already open at {url}."));
        Open(url);
        return true;
    }

    /// <summary>The Windows console uses another code page by default; without this accents break.</summary>
    public static void ConfigureConsole()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = Title;
        }
        catch (Exception)
        {
            // no console (service): ignore
        }
    }

    public static void OpenBrowserWhenStarted(WebApplication app)
    {
        var url = AppUrl(app.Configuration);
        var afterUpdate = app.Configuration.GetValue<bool>(AfterUpdateKey);
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine();
            if (afterUpdate) Console.WriteLine(L.ForSystem($"  Atualizado para a versão {AppVersion.Current}.", $"  Updated to version {AppVersion.Current}."));
            Console.WriteLine(L.ForSystem($"  {Title} rodando em {url}", $"  {Title} running at {url}"));
            Console.WriteLine(L.ForSystem("  Feche esta janela para encerrar.", "  Close this window to quit."));
            Console.WriteLine();
            // After an update the tab that asked for it reloads by itself.
            if (!afterUpdate && app.Configuration.GetValue("Desktop:OpenBrowser", true)) Open(url);
        });
    }

    private const string AfterUpdateKey = "Desktop:AfterUpdate";
    private const string WaitPidKey = "Desktop:WaitPid";

    /// <summary>
    /// A restart after an update starts while the old process is still shutting down: wait for it to free
    /// the port, otherwise <see cref="TryReuseRunningInstanceAsync"/> would find it and exit.
    /// </summary>
    public static void WaitForPreviousInstance(IConfiguration configuration)
    {
        if (configuration.GetValue<int?>(WaitPidKey) is not { } pid) return;
        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // already gone
        }
    }

    /// <summary>Starts the (already replaced) executable in a new console and stops this instance.</summary>
    public static void RestartAfterUpdate(WebApplication app, string exePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Arguments = $"--{AfterUpdateKey}=true --{WaitPidKey}={Environment.ProcessId}",
            });
            Console.WriteLine(L.ForSystem("  Atualização instalada. Reiniciando...", "  Update installed. Restarting..."));
        }
        catch (Exception ex)
        {
            Console.WriteLine(L.ForSystem($"  Atualização instalada, mas não foi possível reabrir o programa: {ex.Message}", $"  Update installed, but the program couldn't be reopened: {ex.Message}"));
            Console.WriteLine(L.ForSystem("  Abra o NightCityItemDB.exe de novo.", "  Open NightCityItemDB.exe again."));
        }
        app.Lifetime.StopApplication();
    }

    /// <summary>Startup failures would otherwise close the console before the user can read them.</summary>
    public static void ReportFatal(Exception exception, IConfiguration configuration)
    {
        Console.WriteLine();
        var portInUse = exception is IOException or System.Net.Sockets.SocketException ||
                        exception.InnerException is System.Net.Sockets.SocketException;
        Console.WriteLine(portInUse
            ? L.ForSystem($"  Não foi possível abrir {AppUrl(configuration)}: a porta já está em uso por outro programa.", $"  Couldn't open {AppUrl(configuration)}: the port is already in use by another program.")
            : L.ForSystem($"  O programa não conseguiu iniciar: {exception.Message}", $"  The program couldn't start: {exception.Message}"));
        Console.WriteLine(L.ForSystem("  Pressione Enter para fechar.", "  Press Enter to close."));
        try { Console.ReadLine(); } catch (Exception) { /* no console */ }
    }

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            Console.WriteLine(L.ForSystem($"Abra no navegador: {url}", $"Open in the browser: {url}"));
        }
    }
}
