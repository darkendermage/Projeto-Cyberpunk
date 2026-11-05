using System.Text.Json.Serialization;

namespace CyberpunkDb.Application.Table;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GridType { Square, None }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenVisibility { All, Gm, Seats }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TemplateShape { Circle, Square, Cone, Line }

/// <summary>Scenery sits over the map and under the characters (barriers, cars, crates); tokens are on top.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenLayer { Token, Scenery }

/// <summary>
/// A barrier from the book's cover table (CP:R p.182–183): material, thickness and HP. On a scene with a fight it is
/// one of the fight's covers (<see cref="CoverId"/>), and the fight keeps its HP.
/// </summary>
public sealed record SceneBarrier
{
    public string Material { get; init; } = "";
    public bool Thick { get; init; }
    public int MaxHp { get; init; }
    public int Hp { get; init; }
    public string? CoverId { get; init; }
}

/// <summary>
/// Everything on a scene, stored as JSON. Positions are in cells: a cell is <see cref="SceneGrid.SizePx"/> pixels of the
/// background image, starting at the grid offset. A scene without a visible grid still measures in cells.
/// </summary>
public sealed record SceneState
{
    public SceneGrid Grid { get; init; } = new();
    public SceneBackground Background { get; init; } = new();
    public List<SceneToken> Tokens { get; init; } = [];
    public List<SceneTemplate> Templates { get; init; } = [];
}

/// <summary>The grid: cell size and offset in image pixels, and how many meters a cell is (2 m in Cyberpunk RED).</summary>
public sealed record SceneGrid
{
    public GridType Type { get; init; } = GridType.Square;
    public double SizePx { get; init; } = 70;
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double MetersPerCell { get; init; } = 2;
    public string Color { get; init; } = "#00f0ff";
    public double Opacity { get; init; } = 0.35;
    /// <summary>Dropped tokens land on whole cells.</summary>
    public bool Snap { get; init; } = true;
}

/// <summary>The map image and its size in pixels (the board's extent).</summary>
public sealed record SceneBackground
{
    public Guid? MediaId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// A token: its top-left corner in cells, its size in cells and its rotation in degrees. It stands for a character
/// (<see cref="CharacterId"/>) or an encounter combatant (<see cref="CombatantId"/>), or is just a marker.
/// </summary>
public sealed record SceneToken
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 1;
    public double Height { get; init; } = 1;
    public double Rotation { get; init; }
    public bool Locked { get; init; }
    public Guid? ImageMediaId { get; init; }
    public string Color { get; init; } = "#00f0ff";
    /// <summary>The seat that moves it; a token of the seat's own character needs none.</summary>
    public int? OwnerSeatId { get; init; }
    public TokenVisibility Visibility { get; init; } = TokenVisibility.All;
    /// <summary>With <see cref="TokenVisibility.Seats"/>: who sees it besides the GM and its owner.</summary>
    public List<int> VisibleSeatIds { get; init; } = [];
    public int? CharacterId { get; init; }
    /// <summary>A saved NPC of the generator the token stands for; its image shows on the token.</summary>
    public int? NpcId { get; init; }
    /// <summary>A named NPC of the books (compendium id) the token stands for; its image shows on the token.</summary>
    public string? CompendiumNpcId { get; init; }
    public string? CombatantId { get; init; }
    /// <summary>A ring drawn around the token, in meters (a weapon's range, a grenade's reach).</summary>
    public double? RangeRingMeters { get; init; }
    public TokenLayer Layer { get; init; } = TokenLayer.Token;
    public SceneBarrier? Barrier { get; init; }
}

/// <summary>
/// An area of effect, measured in meters from its origin (cells): a circle's radius, a square's side, a cone's or a
/// line's length; cones and lines point at <see cref="Angle"/> degrees (0 = right, clockwise).
/// </summary>
public sealed record SceneTemplate
{
    public string Id { get; init; } = "";
    public TemplateShape Shape { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double SizeMeters { get; init; } = 10;
    public double Angle { get; init; }
    public string Color { get; init; } = "#ff2a6d";
    public string? Label { get; init; }
    public bool Locked { get; init; }
    /// <summary>The seat that placed it (players move and delete only their own); null for the GM.</summary>
    public int? OwnerSeatId { get; init; }
}
