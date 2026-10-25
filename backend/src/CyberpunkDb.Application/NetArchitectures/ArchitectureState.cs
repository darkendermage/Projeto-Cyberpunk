using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>
/// Everything about a saved NET Architecture, stored as JSON. <see cref="Branches"/>[0] is the main branch ("A"), whose
/// first two floors are the Lobby and whose last floor is the bottom of the Architecture (where a Virus is left).
/// </summary>
public sealed record ArchitectureState
{
    public int Seed { get; init; }
    public NetDifficulty Difficulty { get; init; }
    public string LocationId { get; init; } = "generic";
    public bool RespectNodeLimit { get; init; } = true;
    public List<NetBranch> Branches { get; init; } = [];
    public List<NetDemon> Demons { get; init; } = [];
    /// <summary>Enemy Netrunners jacked in (Going Quiet Watchers). Demons are Watchers too, but live in <see cref="Demons"/>.</summary>
    public List<NetWatcher> Netrunners { get; init; } = [];
    public string? Notes { get; init; }

    [JsonIgnore] public NetBranch Main => Branches[0];
    [JsonIgnore] public IEnumerable<NetFloor> Floors => Branches.SelectMany(b => b.Floors);
    [JsonIgnore] public int FloorCount => Branches.Sum(b => b.Floors.Count);
    [JsonIgnore] public IEnumerable<NetContent> Contents => Floors.SelectMany(f => f.Contents);
    public NetFloor? Floor(string? id) => Floors.FirstOrDefault(f => f.Id == id);
    public NetFloor? FloorOf(string contentId) => Floors.FirstOrDefault(f => f.Contents.Any(c => c.Id == contentId));
}

/// <summary>
/// A branch of the elevator. The main branch starts at floor 1; any other branch starts right after main floor
/// <see cref="FromFloor"/> (never before the third floor, CP:R p.210).
/// </summary>
public sealed record NetBranch
{
    public string Id { get; init; } = "A";
    public int? FromFloor { get; init; }
    public List<NetFloor> Floors { get; init; } = [];
}

public sealed record NetFloor
{
    /// <summary>Branch letter + depth ("A1", "B4").</summary>
    public string Id { get; init; } = "";
    public string BranchId { get; init; } = "A";
    /// <summary>Depth in the Architecture: how many doors a Netrunner opens to get here.</summary>
    public int Depth { get; init; }
    /// <summary>"Lobby" or a difficulty name. A floor from a harder table than the Architecture's is a Boss Floor.</summary>
    public string Table { get; init; } = "";
    public int? Roll { get; init; }
    public bool Locked { get; init; }
    public List<NetContent> Contents { get; init; } = [];
}

public sealed record NetContent
{
    public string Id { get; init; } = "";
    public NetContentKind Kind { get; init; }
    /// <summary>Black ICE catalog slug.</summary>
    public string? Slug { get; init; }
    /// <summary>DV of a Password (Backdoor), File (Eye-Dee) or Control Node (Control).</summary>
    public int? Dv { get; init; }
    /// <summary>What the File holds in Meatspace terms.</summary>
    public string? FileIdea { get; init; }
    public NodeFunction? Function { get; init; }
}

/// <summary>What a Control Node runs: a Defense (with its chosen loadout) or a civilian device.</summary>
public sealed record NodeFunction
{
    public string? DefenseId { get; init; }
    public List<string> Loadout { get; init; } = [];
    public string? DeviceId { get; init; }
    /// <summary>Free description of what the node controls (overrides the device label).</summary>
    public string? Label { get; init; }
    /// <summary>The Demon that operates this node, if any.</summary>
    public string? DemonId { get; init; }
}

public sealed record NetDemon
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "imp";
    /// <summary>Where the Demon waits (Going Quiet p.5: Demons stick close to a Control Node).</summary>
    public string? FloorId { get; init; }
}

public sealed record NetWatcher
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Interface { get; init; }
    public int PathfinderBonus { get; init; }
    /// <summary>Netrunners like to hang out on the bottom floor (Going Quiet p.5).</summary>
    public string? FloorId { get; init; }
}
