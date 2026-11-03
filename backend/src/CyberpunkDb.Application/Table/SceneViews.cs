using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

/// <summary>Who sees and moves which token, and the scene as the GM or one seat sees it.</summary>
public static class SceneViews
{
    /// <summary>A player moves their own tokens (owned by the seat or standing for its character) unless locked.</summary>
    public static bool CanControl(SceneToken token, TableSeat seat) =>
        !token.Locked && IsOwn(token, seat);

    public static bool IsOwn(SceneToken token, TableSeat seat) =>
        token.OwnerSeatId == seat.Id || (token.CharacterId is not null && token.CharacterId == seat.CharacterId);

    /// <summary>The GM sees everything; a seat sees public tokens, its own, and the ones shown to it.</summary>
    public static bool CanSee(SceneToken token, TableSeat seat) => token.Visibility switch
    {
        TokenVisibility.All => true,
        TokenVisibility.Seats => token.VisibleSeatIds.Contains(seat.Id) || IsOwn(token, seat),
        _ => false,
    };

    public static SceneView ForGm(Scene scene, SceneState state, IReadOnlySet<int> portraits) =>
        View(scene, state, state.Tokens.Select(t => Token(t, GmImage(t, portraits), true)));

    /// <summary>
    /// The seat's view. Portraits go through the player API with the seat link in the query string, because images
    /// load without custom headers.
    /// </summary>
    public static SceneView ForSeat(Scene scene, SceneState state, TableSeat seat, IReadOnlySet<int> portraits) =>
        View(scene, state, state.Tokens.Where(t => CanSee(t, seat)).Select(t => Token(t, SeatImage(t, seat, portraits), CanControl(t, seat))));

    private static SceneView View(Scene scene, SceneState state, IEnumerable<TokenView> tokens) =>
        new(scene.Id, scene.Name, scene.Version, scene.Active, scene.EncounterId, state.Grid,
            new BackgroundView(state.Background.MediaId, state.Background.MediaId is { } m ? MediaService.UrlOf(m) : null, state.Background.Width, state.Background.Height),
            tokens.ToList(), state.Templates, scene.UpdatedAt);

    private static TokenView Token(SceneToken t, string? image, bool controllable) =>
        new(t.Id, t.Label, t.X, t.Y, t.Width, t.Height, t.Rotation, t.Locked, t.ImageMediaId, image, t.Color, t.OwnerSeatId,
            t.Visibility, t.VisibleSeatIds, t.CharacterId, t.CombatantId, t.RangeRingMeters, controllable);

    private static string? GmImage(SceneToken t, IReadOnlySet<int> portraits) =>
        t.ImageMediaId is { } m ? MediaService.UrlOf(m)
        : t.CharacterId is { } c && portraits.Contains(c) ? $"/api/characters/{c}/portrait" : null;

    private static string? SeatImage(SceneToken t, TableSeat seat, IReadOnlySet<int> portraits) =>
        t.ImageMediaId is { } m ? MediaService.UrlOf(m)
        : t.CharacterId is { } c && portraits.Contains(c) ? $"/api/table/play/tokens/{Uri.EscapeDataString(t.Id)}/portrait?seat={Uri.EscapeDataString(seat.Token)}" : null;
}
