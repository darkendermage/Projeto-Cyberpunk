using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table;
using Microsoft.AspNetCore.Http.Features;

namespace CyberpunkDb.Api.Table;

// "Minha ficha" through the tunnel: with a valid seat key the players' port reaches the character API for that
// seat's character only, plus the reads the sheet screens need. No delete/duplicate/export/import/create/list.
public static class SeatCharacterAccess
{
    // anything else is left to the guard
    public static bool IsCandidate(PathString path) =>
        path.StartsWithSegments("/api/characters") || path.StartsWithSegments("/api/items");

    public static async Task<bool> AllowedAsync(HttpContext context)
    {
        var request = context.Request;
        // encoded dots or separators never reach a sheet: the path must be exactly what routing sees
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? request.Path.Value ?? "";
        if (raw.Contains("%2e", StringComparison.OrdinalIgnoreCase) || raw.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("%5c", StringComparison.OrdinalIgnoreCase)) return false;
        var segments = (request.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments.Any(s => s is "." or "..")) return false;

        var token = request.Headers["X-Seat"].FirstOrDefault() ?? request.Query["seat"].FirstOrDefault();
        if (string.IsNullOrEmpty(token)) return false;
        Domain.Entities.TableSeat seat;
        try
        {
            seat = await context.RequestServices.GetRequiredService<SeatService>().AuthenticateAsync(token, context.RequestAborted);
        }
        catch (UnauthorizedException)
        {
            return false;
        }
        if (seat.CharacterId is not { } own) return false;

        var get = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
        // /api/items/{id}: an item's details (the cyberware and store screens).
        if (segments[1].Equals("items", StringComparison.OrdinalIgnoreCase))
            return get && segments.Length == 3 && int.TryParse(segments[2], out _);

        // /api/characters/rules and /api/characters/store: reads every sheet screen uses.
        if (segments.Length == 3 && (segments[2].Equals("rules", StringComparison.OrdinalIgnoreCase) || segments[2].Equals("store", StringComparison.OrdinalIgnoreCase)))
            return get;

        // /api/characters/{own}/...: only the seat's character
        if (segments.Length < 3 || !int.TryParse(segments[2], out var id) || id != own) return false;
        if (segments.Length == 3) return get || HttpMethods.IsPut(request.Method);
        var action = segments[3];
        return !action.Equals("duplicate", StringComparison.OrdinalIgnoreCase) && !action.Equals("export", StringComparison.OrdinalIgnoreCase);
    }
}
