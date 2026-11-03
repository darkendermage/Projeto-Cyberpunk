using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Table;

/// <summary>
/// The players reach the app through a public tunnel, so their port answers only what the virtual table needs: the
/// player API (which also checks the seat link), the table hub, uploaded images and the frontend files. Everything
/// else — the catalog, sheets, the updater — answers 404 there. The tunnel arrives from 127.0.0.1, so the port, not
/// the address, tells the two apart.
///
/// On the GM's port, the table routes refuse cross-site requests: a WebSocket skips CORS, and a page on another site
/// must not drive the table as the GM. They also demand a loopback Host, so a site whose own name was re-pointed to
/// 127.0.0.1 (DNS rebinding: its Origin then matches the Host) is refused too.
/// </summary>
public sealed class PlayerSurfaceGuard(RequestDelegate next, IOptions<TableOptions> options, IConfiguration configuration)
{
    private readonly string[] _origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (options.Value.IsPlayerSurface(context))
        {
            if (!PlayerMayReach(request))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }
        else if (IsTableRoute(request.Path) && (!IsLoopback(request.Host) || !SameSite(request)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }

    private static bool PlayerMayReach(HttpRequest request)
    {
        var path = request.Path;
        if (path.StartsWithSegments("/api"))
            return path.StartsWithSegments("/api/table/play")
                   || (path.StartsWithSegments("/api/table/media") && (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)));
        if (path.StartsWithSegments("/hubs")) return path.StartsWithSegments("/hubs/table");
        if (path.StartsWithSegments("/swagger")) return false;
        // The frontend bundle and its index.html: read-only files.
        return HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
    }

    private static bool IsLoopback(HostString host) =>
        host.Host is "127.0.0.1" or "[::1]" or "::1" || host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsTableRoute(PathString path) => path.StartsWithSegments("/hubs") || path.StartsWithSegments("/api/table");

    /// <summary>No Origin (same-origin GET, tools, tests), this host itself, or the configured dev origins.</summary>
    private bool SameSite(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase)) return true;
        return _origins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
    }
}
