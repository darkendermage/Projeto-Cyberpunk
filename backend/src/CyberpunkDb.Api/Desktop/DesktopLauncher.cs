using System.Diagnostics;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Api.Desktop;

// opens the browser and avoids two instances on the same port
public static class DesktopLauncher
{
    public const string Title = "Night City Item Database";

    public static string AppUrl(IConfiguration configuration) =>
        (configuration["Urls"] ?? "http://localhost:5090").Split(';')[0]
            .Replace("0.0.0.0", "localhost").Replace("*", "localhost").TrimEnd('/');

    // if something already answers, open the browser there and return true
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

    // the Windows console uses another code page and breaks accents otherwise
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
            // after an update the tab that asked for it reloads by itself
            if (!afterUpdate && app.Configuration.GetValue("Desktop:OpenBrowser", true)) Open(url);
        });
    }

    private const string AfterUpdateKey = "Desktop:AfterUpdate";
    private const string WaitPidKey = "Desktop:WaitPid";

    // After an update the old process may still be shutting down. Wait for the port, otherwise
    // TryReuseRunningInstanceAsync would find it and exit.
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

    // the exe was already replaced
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

    // otherwise the console closes before anyone can read the error
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
