using System.Diagnostics;

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

        Console.WriteLine($"O {Title} já está aberto em {url}.");
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
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine();
            Console.WriteLine($"  {Title} rodando em {url}");
            Console.WriteLine("  Feche esta janela para encerrar.");
            Console.WriteLine();
            if (app.Configuration.GetValue("Desktop:OpenBrowser", true)) Open(url);
        });
    }

    /// <summary>Startup failures would otherwise close the console before the user can read them.</summary>
    public static void ReportFatal(Exception exception, IConfiguration configuration)
    {
        Console.WriteLine();
        var portInUse = exception is IOException or System.Net.Sockets.SocketException ||
                        exception.InnerException is System.Net.Sockets.SocketException;
        Console.WriteLine(portInUse
            ? $"  Não foi possível abrir {AppUrl(configuration)}: a porta já está em uso por outro programa."
            : $"  O programa não conseguiu iniciar: {exception.Message}");
        Console.WriteLine("  Pressione Enter para fechar.");
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
            Console.WriteLine($"Abra no navegador: {url}");
        }
    }
}
