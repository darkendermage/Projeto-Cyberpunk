using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures.Rules;

/// <summary>
/// The parameters of <c>net-architecture-rules.json</c>: the Core's NET Architecture generation (CP:R p.210-211),
/// Demons and Defenses (p.212-216), prices (p.217-218) and the Going Quiet stealth rules. Every value is a
/// <see cref="Ruled{T}"/> with its page or the <c>complementary</c> flag.
/// </summary>
public sealed record NetArchitectureRulesDocument
{
    public string About { get; init; } = "";
    public Dictionary<string, string> Books { get; init; } = [];
    public Ruled<DifficultyRule[]> Difficulties { get; init; } = new();
    public ShapeRules Shape { get; init; } = new();
    public Ruled<TableRow[]> LobbyTable { get; init; } = new();
    public Ruled<BodyTable[]> BodyTables { get; init; } = new();
    public Ruled<NetContentKind[]> NoRepeat { get; init; } = new();
    public Ruled<bool> BossFloor { get; init; } = new();
    public Ruled<bool> SameCostSwap { get; init; } = new();
    public Ruled<bool> ActiveNeedsDemon { get; init; } = new();
    public Ruled<DemonRule[]> Demons { get; init; } = new();
    public Ruled<string> DemonAbilities { get; init; } = new();
    public Ruled<int> FloorsPerDemon { get; init; } = new();
    public Ruled<bool> DemonsShareFloor { get; init; } = new();
    public Ruled<DefenseRule[]> Defenses { get; init; } = new();
    public Ruled<string> DefenseControl { get; init; } = new();
    public Ruled<DeviceRule[]> Devices { get; init; } = new();
    public PricingRules Pricing { get; init; } = new();
    public Ruled<LocationRule[]> Locations { get; init; } = new();
    public StealthRules Stealth { get; init; } = new();
    public NetGeneratorRules Generator { get; init; } = new();

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}

public sealed record DifficultyRule(NetDifficulty Id, string Label, int Dv, int FightingChance, int? MightDie, string Description);

public sealed record DiceSpec(int Count, int Sides);

public sealed record ShapeRules
{
    public Ruled<DiceSpec> FloorDice { get; init; } = new();
    public Ruled<int> BranchDie { get; init; } = new();
    public Ruled<int> BranchOn { get; init; } = new();
    public Ruled<int> LobbyFloors { get; init; } = new();
    public Ruled<int> FirstBranchFloor { get; init; } = new();
    public Ruled<bool> LongestBranch { get; init; } = new();
    public Ruled<string> Split { get; init; } = new();
}

/// <summary>What a table row puts behind the "door": a Black ICE, a Password, a File or a Control Node.</summary>
public sealed record TableContent(NetContentKind Kind, string? Slug = null, int? Dv = null);

public sealed record TableRow(int Roll, TableContent[] Contents);

public sealed record BodyTable(NetDifficulty Difficulty, TableRow[] Rows);

public sealed record DemonRule(
    string Id, string Name, int Rez, int Interface, int NetActions, int CombatNumber, int Cost, PriceCategory PriceCategory, string Icon);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DefenseGroup { Active, Emplaced, Environmental }

public sealed record DefenseRule(
    string Id, string Name, string Label, DefenseGroup Group, int Page, int CounterDv, string CounterTime, string Trigger,
    string Data, string Description, string[] LoadoutOptions, int LoadoutPicks)
{
    /// <summary>Active Defenses need a Demon (or a Netrunner) on their Control Node to work (CP:R p.213).</summary>
    public bool RequiresDemon => Group == DefenseGroup.Active;
}

public sealed record DeviceRule(string Id, string Label);

public sealed record FloorBand(int Min, int Max, int? MaxControlNodes, bool Portable, int CostPerFloor, PriceCategory PriceCategory);

public sealed record DvCost(int Dv, int Cost, PriceCategory PriceCategory);

public sealed record PricingRules
{
    public Ruled<FloorBand[]> Floors { get; init; } = new();
    public Ruled<DvCost[]> Features { get; init; } = new();
    public Ruled<int[]> IceMultiplier { get; init; } = new();
    public Ruled<bool> IceSameAsDeck { get; init; } = new();
    public Ruled<DvCost[]> Defenses { get; init; } = new();
    public Ruled<bool> DevicesFree { get; init; } = new();
    public Ruled<string> Availability { get; init; } = new();
}

public sealed record LocationRule(
    string Id, string Label, string Description, double DefenseShare, double DemonChance,
    Dictionary<string, double> Defenses, Dictionary<string, double> Devices, string[] FileIdeas);

public sealed record StealthCheck(string Label, string Roll);

public sealed record StealthRules
{
    public Ruled<string> Watchers { get; init; } = new();
    public Ruled<StealthCheck[]> Checks { get; init; } = new();
    public Ruled<string> Placement { get; init; } = new();
    public Ruled<string> EnemyNetrunnerInterface { get; init; } = new();
}

public sealed record NetGeneratorRules
{
    public Ruled<Dictionary<NetDifficulty, double>> DifficultyWeights { get; init; } = new();
    public Ruled<bool> RespectNodeLimit { get; init; } = new();
    public Ruled<int> NodesPerDemon { get; init; } = new();
    public Ruled<Dictionary<NetDifficulty, Dictionary<string, double>>> DemonWeights { get; init; } = new();
    public Ruled<Dictionary<NetDifficulty, double>> EnemyNetrunnerChance { get; init; } = new();
}
