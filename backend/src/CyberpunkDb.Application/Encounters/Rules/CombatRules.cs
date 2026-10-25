using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters.Rules;

/// <summary>
/// The parameters of <c>combat-rules.json</c> (Friday Night Firefight, CP:R p.168-188). Autofire, Critical Injuries,
/// Wound States and Death Saves are not repeated here: they come from the character rules, the same the sheet uses.
/// </summary>
public sealed record CombatRulesDocument
{
    public string About { get; init; } = "";
    public Dictionary<string, string> Books { get; init; } = [];
    public Ruled<string> Initiative { get; init; } = new();
    public Ruled<string> InitiativeTies { get; init; } = new();
    public Ruled<string> Turn { get; init; } = new();
    public Ruled<ActionInfo[]> Actions { get; init; } = new();
    public Ruled<RangeBand[]> RangeBands { get; init; } = new();
    public Ruled<RangeRow[]> SingleShot { get; init; } = new();
    public Ruled<int> DodgeRef { get; init; } = new();
    public Ruled<bool> DefenderWinsTies { get; init; } = new();
    public Ruled<AimedShotRule> AimedShot { get; init; } = new();
    public Ruled<AimedEffect[]> AimedEffects { get; init; } = new();
    public Ruled<MeleeRule> Melee { get; init; } = new();
    public Ruled<UnarmedDamage[]> UnarmedDamage { get; init; } = new();
    public Ruled<string> CyberarmMinimum { get; init; } = new();
    public Ruled<bool> BrawlingHalvesArmor { get; init; } = new();
    public Ruled<bool> MartialArtsHalvesArmor { get; init; } = new();
    public Ruled<string> Grapple { get; init; } = new();
    public Ruled<ThrownRule> Thrown { get; init; } = new();
    public Ruled<ShotgunShellRule> ShotgunShell { get; init; } = new();
    public Ruled<string> Explosives { get; init; } = new();
    public Ruled<SuppressiveRule> Suppressive { get; init; } = new();
    public Ruled<CoverMaterial[]> CoverMaterials { get; init; } = new();
    public Ruled<CoverExample[]> CoverExamples { get; init; } = new();
    public Ruled<string> CoverRules { get; init; } = new();
    public Ruled<string> Shields { get; init; } = new();
    public Ruled<EnvironmentDamage[]> Environment { get; init; } = new();
    public Ruled<bool> EnvironmentNoCrits { get; init; } = new();
    public Ruled<GenericCombatant> Generic { get; init; } = new();
    public Ruled<bool> NpcAmmo { get; init; } = new();

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}

public sealed record ActionInfo(string Name, string Label, string Text);
public sealed record RangeBand(int Min, int Max);
public sealed record RangeRow(string Key, string Label, WeaponClass[] Classes, int?[] Dv);
public sealed record AimedShotRule(int Penalty, int MaxRof, int HeadMultiplier, int CrackedSkullMultiplier);
public sealed record AimedEffect(AttackMode Mode, string Label, string Effect);
public sealed record MeleeRule(string Check, bool HalveArmor, int ReachMeters, int VeryHeavyRof);
public sealed record UnarmedDamage(int? MaxBody, string Dice);
public sealed record ThrownRule(int MaxMeters, string Row, string Check);
public sealed record ShotgunShellRule(int Dv, string Damage, int RangeMeters);
public sealed record SuppressiveRule(string Check, int RangeMeters, string Effect);
public sealed record CoverMaterial(string Id, string Label, int Thick, int Thin);
public sealed record CoverExample(string Label, string Material, bool Thick, int Hp);
public sealed record EnvironmentDamage(string Id, string Label, string Damage, bool BypassArmor, string Note);
public sealed record GenericCombatant(
    int Ref, int Dex, int Body, int Will, int Move, int Hp, int SpBody, int SpHead, int AttackBase, int EvasionBase, string Damage, WeaponClass WeaponClass);

/// <summary>How an attack is made (CP:R p.170-178).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttackMode { Single, Autofire, AimedHead, AimedHand, AimedLeg, ShotgunShell, Explosive, Suppressive }

/// <summary>The loaded combat rules. Singleton.</summary>
public sealed class CombatRuleBook
{
    private const string Resource = "CyberpunkDb.Application.Encounters.Rules.combat-rules.json";
    private readonly Dictionary<WeaponClass, RangeRow> _rows;

    public CombatRuleBook(CombatRulesDocument document)
    {
        Document = document;
        _rows = document.SingleShot.Value.SelectMany(r => r.Classes.Select(c => (c, r))).ToDictionary(x => x.c, x => x.r);
    }

    public CombatRulesDocument Document { get; }

    public static CombatRuleBook LoadEmbedded()
    {
        using var stream = typeof(CombatRuleBook).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"Recurso {Resource} não encontrado.");
        return new CombatRuleBook(JsonSerializer.Deserialize<CombatRulesDocument>(stream, CombatRulesDocument.JsonOptions)
                                  ?? throw new InvalidOperationException("combat-rules.json vazio."));
    }

    public RangeRow? RowFor(WeaponClass weaponClass) => _rows.GetValueOrDefault(weaponClass);
    public RangeRow Row(string key) => Document.SingleShot.Value.First(r => r.Key == key);

    /// <summary>Index of the range band (CP:R p.173) for a distance in m/yds; null beyond 800.</summary>
    public int? Band(int meters) => Document.RangeBands.Value.Select((b, i) => (b, i)).Where(x => meters >= x.b.Min && meters <= x.b.Max)
        .Select(x => (int?)x.i).FirstOrDefault();

    /// <summary>The DV of a single shot at this range, or an explanation of why the shot can't be made.</summary>
    public int SingleShotDv(RangeRow row, int meters)
    {
        var band = Band(meters) ?? throw RequestValidationException.For("range", $"{meters} m está além da tabela de alcance ({Document.SingleShot.Source}).");
        return row.Dv[band] ?? throw RequestValidationException.For("range",
            $"{row.Label} não alcança {meters} m (N/A na tabela, {Document.SingleShot.Source}).");
    }

    /// <summary>Brawling and Martial Arts damage by BODY (CP:R p.176, p.178); a Cyberarm makes Brawling at least 2d6.</summary>
    public string UnarmedDice(int body, bool cyberarm)
    {
        var dice = Document.UnarmedDamage.Value.First(d => d.MaxBody is null || body <= d.MaxBody).Dice;
        return cyberarm && DiceCount(dice) < DiceCount(Document.CyberarmMinimum.Value) ? Document.CyberarmMinimum.Value : dice;
    }

    public int CoverHp(string material, bool thick)
    {
        var m = Document.CoverMaterials.Value.FirstOrDefault(c => c.Id == material)
                ?? throw RequestValidationException.For("material", $"Material '{material}' não existe.");
        return thick ? m.Thick : m.Thin;
    }

    private static int DiceCount(string dice) => int.TryParse(dice.Split('d')[0], out var n) ? n : 0;
}
