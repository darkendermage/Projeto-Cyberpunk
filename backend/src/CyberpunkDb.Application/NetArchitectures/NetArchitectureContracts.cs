using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures;

// null fields get rolled (the seed makes it reproducible)
public sealed record GenerateArchitectureRequest
{
    public string? Name { get; init; }
    public NetDifficulty? Difficulty { get; init; }
    public int? Floors { get; init; }
    public int? Branches { get; init; }
    public string? LocationId { get; init; }
    public int? Seed { get; init; }
    // CP:R p.217, defaults to the rules' setting
    public bool? RespectNodeLimit { get; init; }
    // null = rolled by difficulty
    public bool? EnemyNetrunner { get; init; }
}

public sealed record VersionRequest(int Version);
public sealed record RegenerateArchitectureRequest(int Version, bool KeepShape);
public sealed record UpdateArchitectureRequest(int Version, string Name, string? Notes);
public sealed record LockFloorRequest(int Version, bool Locked);
// Lobby, the Architecture's table, or one step harder (Boss Floor)
public sealed record SetFloorRequest(int Version, string Table, int Roll);
public sealed record SwapIceRequest(int Version, string Slug);
public sealed record SetNodeRequest(int Version, string? DefenseId, List<string>? Loadout, string? DeviceId, string? Label, string? DemonId);
public sealed record DemonRequest(int Version, string Type, string? FloorId);
public sealed record NetrunnerRequest(int Version, string Name, int Interface, int PathfinderBonus, string? FloorId);

public sealed record CostLine(string Group, string Label, int Quantity, int Unit, int Total, string? Source);

// CP:R p.217-218
public sealed record ArchitectureQuote(
    IReadOnlyList<CostLine> Lines, int Total, bool Portable, int? MaxControlNodes, int ControlNodes, int MaxDemons,
    IReadOnlyList<string> Warnings);

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
