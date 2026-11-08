using System.Text.Json.Serialization;

namespace CyberpunkDb.Application.Table;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GridType { Square, None }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenVisibility { All, Gm, Seats }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TemplateShape { Circle, Square, Cone, Line }

// scenery: over the map, under the tokens
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenLayer { Token, Scenery }

// Cover table barrier (CP:R p.182-183). With a fight it's one of its covers and the fight keeps the HP.
public sealed record SceneBarrier
{
    public string Material { get; init; } = "";
    public bool Thick { get; init; }
    public int MaxHp { get; init; }
    public int Hp { get; init; }
    public string? CoverId { get; init; }
}

// Stored as JSON. Positions are in cells (SceneGrid.SizePx image pixels each, from the grid offset), grid shown or not.
public sealed record SceneState
{
    public SceneGrid Grid { get; init; } = new();
    public SceneBackground Background { get; init; } = new();
    public List<SceneToken> Tokens { get; init; } = [];
    public List<SceneTemplate> Templates { get; init; } = [];
    // below every token, drawn by Order
    public List<SceneImage> Images { get; init; } = [];
}

// Center/size in cells (fractions allowed). Higher Order is on top. OwnerSeatId null = GM.
// Locked stops move/resize; LockedByGm means only the GM can unlock.
public sealed record SceneImage
{
    public string Id { get; init; } = "";
    public Guid MediaId { get; init; }
    public string Name { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 4;
    public double Height { get; init; } = 4;
    public double Rotation { get; init; }
    public double Opacity { get; init; } = 1;
    public int Order { get; init; }
    public bool Locked { get; init; }
    public bool LockedByGm { get; init; }
    public bool Hidden { get; init; }
    public int? OwnerSeatId { get; init; }
}

// offsets in image pixels; 2 m per cell in RED
public sealed record SceneGrid
{
    public GridType Type { get; init; } = GridType.Square;
    public double SizePx { get; init; } = 70;
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double MetersPerCell { get; init; } = 2;
    public string Color { get; init; } = "#00f0ff";
    public double Opacity { get; init; } = 0.35;
    // snap drops to whole cells
    public bool Snap { get; init; } = true;
}

public sealed record SceneBackground
{
    public Guid? MediaId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

// Top-left corner and size in cells, rotation in degrees. Points to a character, a combatant, or nothing.
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
    // tokens of the seat's own character don't need it
    public int? OwnerSeatId { get; init; }
    public TokenVisibility Visibility { get; init; } = TokenVisibility.All;
    // only for TokenVisibility.Seats
    public List<int> VisibleSeatIds { get; init; } = [];
    public int? CharacterId { get; init; }
    // saved generator NPC
    public int? NpcId { get; init; }
    // compendium id
    public string? CompendiumNpcId { get; init; }
    public string? CombatantId { get; init; }
    // weapon range, grenade reach...
    public double? RangeRingMeters { get; init; }
    public TokenLayer Layer { get; init; } = TokenLayer.Token;
    public SceneBarrier? Barrier { get; init; }
}

// Meters from the origin: circle radius, square side, cone/line length. Angle 0 = right, clockwise.
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
    // null = GM
    public int? OwnerSeatId { get; init; }
}
