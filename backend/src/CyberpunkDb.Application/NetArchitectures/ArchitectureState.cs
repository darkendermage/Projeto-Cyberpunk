using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures;

// Stored as JSON. Branches[0] is the main branch ("A"): first two floors are the Lobby, the last one is the
// bottom (where a Virus gets left).
public sealed record ArchitectureState
{
    public int Seed { get; init; }
    public NetDifficulty Difficulty { get; init; }
    public string LocationId { get; init; } = "generic";
    public bool RespectNodeLimit { get; init; } = true;
    public List<NetBranch> Branches { get; init; } = [];
    public List<NetDemon> Demons { get; init; } = [];
    // Going Quiet Watchers. Demons are Watchers too but live in Demons.
    public List<NetWatcher> Netrunners { get; init; } = [];
    public string? Notes { get; init; }

    [JsonIgnore] public NetBranch Main => Branches[0];
    [JsonIgnore] public IEnumerable<NetFloor> Floors => Branches.SelectMany(b => b.Floors);
    [JsonIgnore] public int FloorCount => Branches.Sum(b => b.Floors.Count);
    [JsonIgnore] public IEnumerable<NetContent> Contents => Floors.SelectMany(f => f.Contents);
    public NetFloor? Floor(string? id) => Floors.FirstOrDefault(f => f.Id == id);
    public NetFloor? FloorOf(string contentId) => Floors.FirstOrDefault(f => f.Contents.Any(c => c.Id == contentId));
}

// Main branch starts at floor 1; others start right after main floor FromFloor (never before the 3rd, CP:R p.210).
public sealed record NetBranch
{
    public string Id { get; init; } = "A";
    public int? FromFloor { get; init; }
    public List<NetFloor> Floors { get; init; } = [];
}

public sealed record NetFloor
{
    // "A1", "B4"
    public string Id { get; init; } = "";
    public string BranchId { get; init; } = "A";
    // doors a Netrunner opens to get here
    public int Depth { get; init; }
    // "Lobby" or a difficulty. A harder table than the Architecture's = Boss Floor.
    public string Table { get; init; } = "";
    public int? Roll { get; init; }
    public bool Locked { get; init; }
    public List<NetContent> Contents { get; init; } = [];
}

public sealed record NetContent
{
    public string Id { get; init; } = "";
    public NetContentKind Kind { get; init; }
    public string? Slug { get; init; }
    // Password (Backdoor), File (Eye-Dee) or Control Node (Control)
    public int? Dv { get; init; }
    // in Meatspace terms
    public string? FileIdea { get; init; }
    public NodeFunction? Function { get; init; }
}

// a Defense (with its loadout) or a civilian device
public sealed record NodeFunction
{
    public string? DefenseId { get; init; }
    public List<string> Loadout { get; init; } = [];
    public string? DeviceId { get; init; }
    // overrides the device label
    public string? Label { get; init; }
    public string? DemonId { get; init; }
}

public sealed record NetDemon
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "imp";
    // Demons stick close to a Control Node (Going Quiet p.5)
    public string? FloorId { get; init; }
}

public sealed record NetWatcher
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Interface { get; init; }
    public int PathfinderBonus { get; init; }
    // they like the bottom floor (Going Quiet p.5)
    public string? FloorId { get; init; }
}
