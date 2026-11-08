using System.Text.Json;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NetArchitectures.Rules;

public sealed class NetArchitectureRuleBook
{
    private const string Resource = "CyberpunkDb.Application.NetArchitectures.Rules.net-architecture-rules.json";
    public const string LobbyTable = "Lobby";

    private readonly Dictionary<NetDifficulty, DifficultyRule> _difficulties;
    private readonly Dictionary<NetDifficulty, BodyTable> _bodies;
    private readonly Dictionary<string, DemonRule> _demons;
    private readonly Dictionary<string, DefenseRule> _defenses;
    private readonly Dictionary<string, DeviceRule> _devices;
    private readonly Dictionary<string, LocationRule> _locations;

    public NetArchitectureRuleBook(NetArchitectureRulesDocument document)
    {
        Document = document;
        _difficulties = document.Difficulties.Value.ToDictionary(d => d.Id);
        _bodies = document.BodyTables.Value.ToDictionary(t => t.Difficulty);
        _demons = document.Demons.Value.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        _defenses = document.Defenses.Value.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        _devices = document.Devices.Value.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        _locations = document.Locations.Value.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);
    }

    public NetArchitectureRulesDocument Document { get; }
    public IReadOnlyList<DifficultyRule> Difficulties => Document.Difficulties.Value;
    public IReadOnlyList<DemonRule> Demons => Document.Demons.Value;
    public IReadOnlyList<DefenseRule> Defenses => Document.Defenses.Value;
    public IReadOnlyList<DeviceRule> Devices => Document.Devices.Value;
    public IReadOnlyList<LocationRule> Locations => Document.Locations.Value;

    public static NetArchitectureRuleBook LoadEmbedded()
    {
        using var stream = typeof(NetArchitectureRuleBook).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"Recurso {Resource} não encontrado.");
        return new NetArchitectureRuleBook(JsonSerializer.Deserialize<NetArchitectureRulesDocument>(stream, NetArchitectureRulesDocument.JsonOptions)
                                           ?? throw new InvalidOperationException("net-architecture-rules.json vazio."));
    }

    public DifficultyRule Difficulty(NetDifficulty id) => _difficulties[id];

    // Boss Floor (CP:R p.211); null above Advanced
    public static NetDifficulty? Harder(NetDifficulty id) => id == NetDifficulty.Advanced ? null : id + 1;

    // "Lobby" or a difficulty name
    public IReadOnlyList<TableRow> Table(string table) =>
        table == LobbyTable ? Document.LobbyTable.Value
        : Enum.TryParse<NetDifficulty>(table, out var d) && _bodies.TryGetValue(d, out var body) ? body.Rows
        : throw RequestValidationException.For("table", L.T($"Tabela '{table}' não existe.", $"Table '{table}' doesn't exist."));

    public TableRow Row(string table, int roll) => Table(table).FirstOrDefault(r => r.Roll == roll)
        ?? throw RequestValidationException.For("roll", L.T($"A tabela {table} não tem a linha {roll}.", $"Table {table} has no row {roll}."));

    public DemonRule Demon(string id) => _demons.TryGetValue(id, out var d)
        ? d
        : throw RequestValidationException.For("demon", L.T($"Demon '{id}' não existe.", $"Demon '{id}' doesn't exist."));

    public DefenseRule? FindDefense(string? id) => id is null ? null : _defenses.GetValueOrDefault(id);
    public DeviceRule? FindDevice(string? id) => id is null ? null : _devices.GetValueOrDefault(id);

    public LocationRule Location(string? id) => _locations.TryGetValue(id ?? "generic", out var l)
        ? l
        : throw RequestValidationException.For("locationId", L.T($"Tipo de local '{id}' não existe.", $"Location type '{id}' doesn't exist."));

    // CP:R p.217: portability, Control Node limit, price per floor
    public FloorBand Band(int floors) =>
        Document.Pricing.Floors.Value.FirstOrDefault(b => floors >= b.Min && floors <= b.Max)
        ?? Document.Pricing.Floors.Value.OrderBy(b => Math.Abs(b.Min - floors)).First();

    // CP:R p.218, as we read it
    public int MaxDemons(int floors) => (int)Math.Ceiling(floors / (double)Document.FloorsPerDemon.Value);

    public int? FeatureCost(int dv) => Document.Pricing.Features.Value.FirstOrDefault(c => c.Dv == dv)?.Cost;
    public int? DefenseCost(int counterDv) => Document.Pricing.Defenses.Value.FirstOrDefault(c => c.Dv == counterDv)?.Cost;

    // Black ICE and Password rows (CP:R p.210)
    public bool Unique(TableRow row) => row.Contents.Any(c => Document.NoRepeat.Value.Contains(c.Kind));
}
