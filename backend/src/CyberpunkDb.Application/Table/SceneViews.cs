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

    public static SceneView ForGm(Scene scene, SceneState state, TokenImages images, IReadOnlyDictionary<string, int>? covers = null) =>
        View(scene, state, state.Tokens.Select(t => Token(t, GmImage(t, images), true, covers, Focus(t, images))));

    /// <summary>
    /// The seat's view. Stored images go through the player API with the seat link in the query string, because images
    /// load without custom headers.
    /// </summary>
    public static SceneView ForSeat(Scene scene, SceneState state, TableSeat seat, TokenImages images, IReadOnlyDictionary<string, int>? covers = null) =>
        View(scene, state, state.Tokens.Where(t => CanSee(t, seat)).Select(t => Token(t, SeatImage(t, seat, images), CanControl(t, seat), covers, Focus(t, images))));

    /// <summary>The framing of a PC's portrait, when the token shows that portrait.</summary>
    private static CyberpunkDb.Application.Characters.PortraitFocus? Focus(SceneToken t, TokenImages images) =>
        ImageSource(t, images) is { Kind: TokenImageKind.Character } && t.CharacterId is { } c && images.CharacterFocus?.TryGetValue(c, out var f) == true ? f : null;

    /// <summary>
    /// The stored image a token shows when no image was put on the token itself: the character's portrait, else the
    /// saved NPC's image, else the compendium NPC's. Null when there is none (the client draws initials).
    /// </summary>
    public static TokenImageSource? ImageSource(SceneToken t, TokenImages images)
    {
        if (t.ImageMediaId is not null) return null;
        if (t.CharacterId is { } c && images.Characters.TryGetValue(c, out var cs)) return new(TokenImageKind.Character, c.ToString(), cs);
        if (t.NpcId is { } n && images.Npcs.TryGetValue(n, out var ns)) return new(TokenImageKind.Npc, n.ToString(), ns);
        if (t.CompendiumNpcId is { } b && images.Compendium.TryGetValue(b, out var bs)) return new(TokenImageKind.Compendium, b, bs);
        return null;
    }

    private static SceneView View(Scene scene, SceneState state, IEnumerable<TokenView> tokens) =>
        new(scene.Id, scene.Name, scene.Version, scene.Active, scene.EncounterId, state.Grid,
            new BackgroundView(state.Background.MediaId, state.Background.MediaId is { } m ? MediaService.UrlOf(m) : null, state.Background.Width, state.Background.Height),
            tokens.ToList(), state.Templates, scene.UpdatedAt);

    private static TokenView Token(SceneToken t, string? image, bool controllable, IReadOnlyDictionary<string, int>? covers, CyberpunkDb.Application.Characters.PortraitFocus? focus = null) =>
        new(t.Id, t.Label, t.X, t.Y, t.Width, t.Height, t.Rotation, t.Locked, t.ImageMediaId, image, t.Color, t.OwnerSeatId,
            t.Visibility, t.VisibleSeatIds, t.CharacterId, t.CombatantId, t.RangeRingMeters, controllable, t.NpcId, t.CompendiumNpcId,
            t.Layer, t.Barrier is { } b ? new BarrierView(b.Material, b.Thick, b.MaxHp, CoverHp(b, covers), b.CoverId) : null, focus);

    /// <summary>A barrier that is a fight's cover shows the cover's HP (damage is done on the combat table too).</summary>
    private static int CoverHp(SceneBarrier b, IReadOnlyDictionary<string, int>? covers) =>
        b.CoverId is { } id && covers is not null && covers.TryGetValue(id, out var hp) ? hp : b.Hp;

    private static string? GmImage(SceneToken t, TokenImages images)
    {
        if (t.ImageMediaId is { } m) return MediaService.UrlOf(m);
        return ImageSource(t, images) switch
        {
            { Kind: TokenImageKind.Character } s => $"/api/characters/{s.Key}/portrait?v={s.Stamp.Ticks}",
            { Kind: TokenImageKind.Npc } s => $"/api/npcs/{s.Key}/portrait?v={s.Stamp.Ticks}",
            { Kind: TokenImageKind.Compendium } s => $"/api/npc-compendium/{Uri.EscapeDataString(s.Key)}/portrait?v={s.Stamp.Ticks}",
            _ => null,
        };
    }

    private static string? SeatImage(SceneToken t, TableSeat seat, TokenImages images)
    {
        if (t.ImageMediaId is { } m) return MediaService.UrlOf(m);
        return ImageSource(t, images) is { } s
            ? $"/api/table/play/tokens/{Uri.EscapeDataString(t.Id)}/portrait?seat={Uri.EscapeDataString(seat.Token)}&v={s.Stamp.Ticks}"
            : null;
    }
}
