using CyberpunkDb.Application.Encounters;

namespace CyberpunkDb.Application.Table;

public sealed record SceneSummaryDto(int Id, string Name, bool Active, int SortOrder, int? EncounterId, string? BackgroundUrl, int Tokens, DateTime UpdatedAt);

/// <summary>
/// A scene as one viewer sees it: the GM gets every token; a player gets the tokens visible to their seat, with
/// <see cref="TokenView.Controllable"/> marking the ones they may move.
/// </summary>
public sealed record SceneView(
    int Id, string Name, int Version, bool Active, int? EncounterId, SceneGrid Grid, BackgroundView Background,
    IReadOnlyList<TokenView> Tokens, IReadOnlyList<SceneTemplate> Templates, DateTime UpdatedAt);

public sealed record BackgroundView(Guid? MediaId, string? Url, int Width, int Height);

public sealed record TokenView(
    string Id, string Label, double X, double Y, double Width, double Height, double Rotation, bool Locked,
    Guid? ImageMediaId, string? ImageUrl, string Color, int? OwnerSeatId, TokenVisibility Visibility, IReadOnlyList<int> VisibleSeatIds,
    int? CharacterId, string? CombatantId, double? RangeRingMeters, bool Controllable, int? NpcId, string? CompendiumNpcId,
    TokenLayer Layer = TokenLayer.Token, BarrierView? Barrier = null);

/// <summary>A barrier as shown: with a fight, the HP is the fight's cover HP.</summary>
public sealed record BarrierView(string Material, bool Thick, int MaxHp, int Hp, string? CoverId);

/// <summary>A barrier put on the map: a material of the book (CP:R p.182) and its thickness; the HP comes from the table unless given (the examples of p.183).</summary>
public sealed record BarrierRequest(string? Label, string Material, bool Thick, int? Hp, double X, double Y, double Width = 1, double Height = 1);

/// <summary>Damage to a barrier; a negative amount repairs it (only while it is not a fight's cover).</summary>
public sealed record BarrierDamageRequest(int Amount);

public sealed record CreateSceneRequest(string Name);

/// <summary>Scene settings, checked against the version: the map, the grid and the linked encounter.</summary>
public sealed record UpdateSceneRequest(int Version, string Name, SceneGrid Grid, Guid? BackgroundMediaId, int BackgroundWidth, int BackgroundHeight, int? EncounterId);

public sealed record ReorderScenesRequest(List<int> Ids);

/// <summary>Everything about a token the GM sets.</summary>
public sealed record TokenInput(
    string Label, double X, double Y, double Width = 1, double Height = 1, double Rotation = 0, bool Locked = false,
    Guid? ImageMediaId = null, string? Color = null, int? OwnerSeatId = null, TokenVisibility Visibility = TokenVisibility.All,
    List<int>? VisibleSeatIds = null, int? CharacterId = null, string? CombatantId = null, double? RangeRingMeters = null,
    int? NpcId = null, string? CompendiumNpcId = null, TokenLayer Layer = TokenLayer.Token);

/// <summary>A drag, a turn or a resize; anything left null stays as it was. Last write wins.</summary>
public sealed record TokenMoveRequest(double X, double Y, double? Rotation = null, double? Width = null, double? Height = null);

/// <summary>Where one token or area goes, in cells.</summary>
public sealed record BatchMove(string Id, double X, double Y);

/// <summary>
/// A selection changed at once: tokens and areas moved, cleared, hidden or shown, in one save. Players may move what
/// they control and players' areas; clearing and hiding tokens is the GM's.
/// </summary>
public sealed record SceneBatchRequest(
    List<BatchMove>? MoveTokens = null, List<BatchMove>? MoveTemplates = null, List<string>? RemoveTokens = null,
    List<string>? RemoveTemplates = null, List<string>? HideTokens = null, List<string>? ShowTokens = null);

/// <summary>Where a player puts their own token on the live scene, in cells.</summary>
public sealed record PlaceTokenRequest(double X, double Y);

/// <summary>A fight started from the map: the chosen tokens and the side each one fights on.</summary>
public sealed record CreateFightRequest(string? Name, List<FightTokenEntry> Tokens);
public sealed record FightTokenEntry(string TokenId, CombatSide Side);

public sealed record TemplateInput(TemplateShape Shape, double X, double Y, double SizeMeters, double Angle = 0, string? Color = null, string? Label = null, bool Locked = false);
