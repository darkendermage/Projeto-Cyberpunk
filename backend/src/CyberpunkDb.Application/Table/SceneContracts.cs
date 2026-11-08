using CyberpunkDb.Application.Encounters;

namespace CyberpunkDb.Application.Table;

public sealed record SceneSummaryDto(int Id, string Name, bool Active, int SortOrder, int? EncounterId, string? BackgroundUrl, int Tokens, DateTime UpdatedAt);

// GM gets every token, players only what their seat can see (Controllable = can move it).
public sealed record SceneView(
    int Id, string Name, int Version, bool Active, int? EncounterId, SceneGrid Grid, BackgroundView Background,
    IReadOnlyList<TokenView> Tokens, IReadOnlyList<SceneTemplate> Templates, DateTime UpdatedAt,
    IReadOnlyList<SceneImageView>? Images = null);

public sealed record SceneImageView(
    string Id, string Url, string Name, double X, double Y, double Width, double Height, double Rotation, double Opacity, int Order,
    bool Locked, bool LockedByGm, bool Hidden, int? OwnerSeatId, bool Editable);

// MediaId only when adding; no size = 4x4 cells
public sealed record ImageInput(
    Guid? MediaId, string? Name, double X, double Y, double? Width = null, double? Height = null, double Rotation = 0, double Opacity = 1,
    bool Locked = false, bool Hidden = false);

// null = top
public sealed record ImageOrderRequest(string? Above);

public sealed record BackgroundView(Guid? MediaId, string? Url, int Width, int Height);

public sealed record TokenView(
    string Id, string Label, double X, double Y, double Width, double Height, double Rotation, bool Locked,
    Guid? ImageMediaId, string? ImageUrl, string Color, int? OwnerSeatId, TokenVisibility Visibility, IReadOnlyList<int> VisibleSeatIds,
    int? CharacterId, string? CombatantId, double? RangeRingMeters, bool Controllable, int? NpcId, string? CompendiumNpcId,
    TokenLayer Layer = TokenLayer.Token, BarrierView? Barrier = null, CyberpunkDb.Application.Characters.PortraitFocus? ImageFocus = null);

// with a fight, HP is the cover's
public sealed record BarrierView(string Material, bool Thick, int MaxHp, int Hp, string? CoverId);

// material from CP:R p.182; HP from the table unless given (examples on p.183)
public sealed record BarrierRequest(string? Label, string Material, bool Thick, int? Hp, double X, double Y, double Width = 1, double Height = 1);

// negative repairs (only when it isn't a fight cover)
public sealed record BarrierDamageRequest(int Amount);

public sealed record CreateSceneRequest(string Name);

public sealed record UpdateSceneRequest(int Version, string Name, SceneGrid Grid, Guid? BackgroundMediaId, int BackgroundWidth, int BackgroundHeight, int? EncounterId);

public sealed record ReorderScenesRequest(List<int> Ids);

public sealed record TokenInput(
    string Label, double X, double Y, double Width = 1, double Height = 1, double Rotation = 0, bool Locked = false,
    Guid? ImageMediaId = null, string? Color = null, int? OwnerSeatId = null, TokenVisibility Visibility = TokenVisibility.All,
    List<int>? VisibleSeatIds = null, int? CharacterId = null, string? CombatantId = null, double? RangeRingMeters = null,
    int? NpcId = null, string? CompendiumNpcId = null, TokenLayer Layer = TokenLayer.Token);

// null = unchanged, last write wins
public sealed record TokenMoveRequest(double X, double Y, double? Rotation = null, double? Width = null, double? Height = null);

public sealed record BatchMove(string Id, double X, double Y);

// Players can move what they control and players' areas; clearing/hiding tokens is GM only.
public sealed record SceneBatchRequest(
    List<BatchMove>? MoveTokens = null, List<BatchMove>? MoveTemplates = null, List<string>? RemoveTokens = null,
    List<string>? RemoveTemplates = null, List<string>? HideTokens = null, List<string>? ShowTokens = null);

public sealed record PlaceTokenRequest(double X, double Y);

public sealed record CreateFightRequest(string? Name, List<FightTokenEntry> Tokens);
public sealed record FightTokenEntry(string TokenId, CombatSide Side);

public sealed record TemplateInput(TemplateShape Shape, double X, double Y, double SizeMeters, double Angle = 0, string? Color = null, string? Label = null, bool Locked = false);
