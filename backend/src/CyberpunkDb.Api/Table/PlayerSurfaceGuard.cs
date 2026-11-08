using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Table;

// Players come in through a public tunnel, so their port only serves what the table needs: the player API, the
// hub, uploaded images, category list/schemas (Gear tab item page) and the frontend. Everything else is a 404.
// The tunnel arrives from 127.0.0.1, so we tell the two apart by port, not address.
//
// On the GM port the table routes refuse cross-site requests (WebSockets skip CORS) and require a loopback Host,
// which also blocks DNS rebinding.
public sealed class PlayerSurfaceGuard(RequestDelegate next, IOptions<TableOptions> options, IConfiguration configuration)
{
    private readonly string[] _origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (options.Value.IsPlayerSurface(context))
        {
            // "Minha ficha": only with the seat key, only that character
            if (SeatCharacterAccess.IsCandidate(request.Path))
            {
                if (!await SeatCharacterAccess.AllowedAsync(context))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next(context);
                return;
            }
            if (!PlayerMayReach(request))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (IsOtherPage(request.Path))
            {
                context.Response.Redirect(SeatPage);
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
                   || (path.StartsWithSegments("/api/table/media") && (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)))
                   || CatalogPageRead(request);
        if (path.StartsWithSegments("/hubs")) return path.StartsWithSegments("/hubs/table");
        if (path.StartsWithSegments("/swagger")) return false;
        return HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
    }

    // The Gear tab item page also needs the category list and schema. Searches, lists and favorites stay closed.
    private static bool CatalogPageRead(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method)) return false;
        var segments = (request.Path.Value ?? "").Trim('/').Split('/');
        return segments switch
        {
            ["api", "categories"] => true,
            ["api", "categories", _, "schema"] => true,
            _ => false,
        };
    }

    private const string SeatPage = "/mesa/j/";

    // Any other app page (bare tunnel address, catalog...) would load and fail on every refused call, so the player
    // page explains the link instead. Files with an extension are the bundle and pass.
    private static bool IsOtherPage(PathString path) =>
        !path.StartsWithSegments("/api") && !path.StartsWithSegments("/hubs") && !path.StartsWithSegments("/mesa/j")
        && !Path.HasExtension(path.Value ?? "");

    private static bool IsLoopback(HostString host) =>
        host.Host is "127.0.0.1" or "[::1]" or "::1" || host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsTableRoute(PathString path) => path.StartsWithSegments("/hubs") || path.StartsWithSegments("/api/table");

    // no Origin (same-origin GET, tools, tests), this host, or the dev origins
    private bool SameSite(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase)) return true;
        return _origins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
    }
}
