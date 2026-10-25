using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>Options of a new Architecture. Anything left null is rolled (the seed makes it reproducible).</summary>
public sealed record GenerateArchitectureRequest
{
    public string? Name { get; init; }
    public NetDifficulty? Difficulty { get; init; }
    public int? Floors { get; init; }
    public int? Branches { get; init; }
    public string? LocationId { get; init; }
    public int? Seed { get; init; }
    /// <summary>Re-roll Control Nodes past the purchase limit of CP:R p.217 (default: the rules' setting).</summary>
    public bool? RespectNodeLimit { get; init; }
    /// <summary>true: an enemy Netrunner is jacked in; false: none; null: rolled by difficulty.</summary>
    public bool? EnemyNetrunner { get; init; }
}

public sealed record VersionRequest(int Version);
public sealed record RegenerateArchitectureRequest(int Version, bool KeepShape);
public sealed record UpdateArchitectureRequest(int Version, string Name, string? Notes);
public sealed record LockFloorRequest(int Version, bool Locked);
/// <summary>Puts a specific table row on a floor: the Lobby, the Architecture's own table or one step harder (Boss Floor).</summary>
public sealed record SetFloorRequest(int Version, string Table, int Roll);
public sealed record SwapIceRequest(int Version, string Slug);
public sealed record SetNodeRequest(int Version, string? DefenseId, List<string>? Loadout, string? DeviceId, string? Label, string? DemonId);
public sealed record DemonRequest(int Version, string Type, string? FloorId);
public sealed record NetrunnerRequest(int Version, string Name, int Interface, int PathfinderBonus, string? FloorId);

public sealed record CostLine(string Group, string Label, int Quantity, int Unit, int Total, string? Source);

/// <summary>What the Architecture would cost to buy and fill (CP:R p.217-218), with rule warnings.</summary>
public sealed record ArchitectureQuote(
    IReadOnlyList<CostLine> Lines, int Total, bool Portable, int? MaxControlNodes, int ControlNodes, int MaxDemons,
    IReadOnlyList<string> Warnings);

/// <summary>A Going Quiet Watcher: a Demon or an enemy Netrunner with an Interface.</summary>
public sealed record WatcherDto(string Id, string Kind, string Name, int Interface, int PathfinderBonus, string? FloorId);

public sealed record LocationSummary(string Id, string Label);

public sealed record NetArchitectureDetailDto(
    int Id, string Name, int Version, DateTime CreatedAt, DateTime UpdatedAt, ArchitectureState State,
    DifficultyRule Difficulty, LocationSummary Location, string BottomFloorId, ArchitectureQuote Quote,
    IReadOnlyList<IceInfo> Ice, IReadOnlyList<WatcherDto> Watchers);

public sealed record NetArchitectureSummaryDto(
    int Id, string Name, NetDifficulty Difficulty, int Floors, int Branches, LocationSummary Location, int Total,
    bool Portable, DateTime UpdatedAt);

public sealed record NetArchitectureRulesDto(NetArchitectureRulesDocument Rules, IReadOnlyList<IceInfo> Ice);
