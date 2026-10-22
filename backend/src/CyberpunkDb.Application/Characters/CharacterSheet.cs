using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters;

/// <summary>
/// The stored state of a player character: only source data, never derived values. STATs are the base values;
/// temporary effects live in <see cref="Modifiers"/>. Skills are keyed like NPC skills ("handgun",
/// "language:Streetslang") and hold the total level, including the free Cultural Origin language levels.
/// </summary>
public sealed record CharacterSheet
{
    public CharacterIdentity Identity { get; init; } = new();
    /// <summary>The Roles the character has; the first one is the current Role (CP:R p.142).</summary>
    public List<CharacterRole> Roles { get; init; } = [];
    public CreationState Creation { get; init; } = new();
    /// <summary>Base value of the ten STATs, LUCK included. Never overwritten by modifiers.</summary>
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StatModifier> Modifiers { get; init; } = [];
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<InventoryEntry> Inventory { get; init; } = [];
    public List<CharacterCyberware> Cyberware { get; init; } = [];
    public CombatState Combat { get; init; } = new();
    public List<CriticalInjuryRecord> Injuries { get; init; } = [];
    public HumanityState Humanity { get; init; } = new();
    public LifepathState Lifepath { get; init; } = new();
    /// <summary>Answers of each Role-based Lifepath, by Role then question id.</summary>
    public Dictionary<string, Dictionary<string, string>> RoleLifepath { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public StyleState Style { get; init; } = new();
    public HousingState Housing { get; init; } = new();
    public Wallet Wallet { get; init; } = new();
    public ProgressState Progress { get; init; } = new();
    /// <summary>
    /// Data of the Role-specific modules (see <c>Roles/RoleModules.cs</c>), by Role. Kept as raw JSON so modules can be
    /// added later without changing the stored sheets.
    /// </summary>
    public Dictionary<string, System.Text.Json.JsonElement> RoleData { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Notes { get; init; }

    [JsonIgnore]
    public CharacterRole? PrimaryRole => Roles.Count > 0 ? Roles[0] : null;

    public int Stat(string key) => Stats.TryGetValue(key, out var v) ? v : 0;

    /// <summary>Rebuilds dictionaries with case-insensitive keys and fills nulls left by deserialization.</summary>
    public CharacterSheet Normalize() => this with
    {
        Identity = (Identity ?? new()).Normalize(),
        Roles = Roles ?? [],
        Creation = Creation is null ? new() : Creation with
        {
            StatRolls = new Dictionary<string, int>(Creation.StatRolls ?? [], StringComparer.OrdinalIgnoreCase),
            StartingChoices = new Dictionary<string, int>(Creation.StartingChoices ?? [], StringComparer.OrdinalIgnoreCase),
            SkillChoices = new Dictionary<string, string>(Creation.SkillChoices ?? [], StringComparer.OrdinalIgnoreCase),
            Overrides = Creation.Overrides ?? new(),
        },
        Stats = new Dictionary<string, int>(
            (Stats ?? []).ToDictionary(kv => kv.Key.Trim().ToUpperInvariant(), kv => kv.Value), StringComparer.OrdinalIgnoreCase),
        Modifiers = Modifiers ?? [],
        Skills = new Dictionary<string, int>(
            (Skills ?? []).Where(kv => kv.Value > 0)
                .GroupBy(kv => SkillKey.Normalize(kv.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(kv => kv.Value)), StringComparer.OrdinalIgnoreCase),
        Inventory = Inventory ?? [],
        Cyberware = Cyberware ?? [],
        Combat = Combat ?? new(),
        Injuries = Injuries ?? [],
        Humanity = Humanity ?? new(),
        Lifepath = (Lifepath ?? new()).Normalize(),
        RoleLifepath = new Dictionary<string, Dictionary<string, string>>(RoleLifepath ?? [], StringComparer.OrdinalIgnoreCase),
        Style = Style ?? new(),
        Housing = Housing ?? new(),
        Wallet = Wallet ?? new(),
        Progress = Progress ?? new(),
        RoleData = new Dictionary<string, System.Text.Json.JsonElement>(RoleData ?? [], StringComparer.OrdinalIgnoreCase),
    };
}

// ------------------------------------------------------------ identity and roles

public sealed record CharacterIdentity
{
    public string Name { get; init; } = "";
    public string? Handle { get; init; }
    public string? Pronouns { get; init; }
    public int? Age { get; init; }
    public string? Concept { get; init; }
    /// <summary>Cultural Origin language: Language skill at 4 for free (CP:R p.45).</summary>
    public string? NativeLanguage { get; init; }
    /// <summary>Where the character lives: the specialization of Local Expert (Your Home).</summary>
    public string Home { get; init; } = "Watson";
    public int Reputation { get; init; }

    public CharacterIdentity Normalize() => this with
    {
        Name = Name?.Trim() ?? "",
        Handle = Blank(Handle),
        Pronouns = Blank(Pronouns),
        Concept = Blank(Concept),
        NativeLanguage = Blank(NativeLanguage),
        Home = string.IsNullOrWhiteSpace(Home) ? "Watson" : Home.Trim(),
    };

    internal static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// One Role and its Role Ability. Multiclassing adds entries through an explicit action after creation (CP:R p.142);
/// the ability name comes from the rules, never from the sheet.
/// </summary>
public sealed record CharacterRole
{
    public string Role { get; init; } = "";
    public int Rank { get; init; }
    /// <summary>Improvement Points spent on this Role's ranks after creation.</summary>
    public int IpInvested { get; init; }
    public DateTime? AcquiredAt { get; init; }
    public string? Notes { get; init; }
}

// ------------------------------------------------------------ creation

/// <summary>How the character was generated and the choices made, so the method rules can be re-checked.</summary>
public sealed record CreationState
{
    public CreationMethod Method { get; init; }
    public bool Finalized { get; init; }
    public DateTime? FinalizedAt { get; init; }
    /// <summary>Streetrat: the 1d10 row of the Role table (1–10).</summary>
    public int? TemplateRow { get; init; }
    /// <summary>Edgerunner: the 1d10 rolled for each STAT, by STAT key.</summary>
    public Dictionary<string, int> StatRolls { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Starting package alternatives picked, keyed "section:group" (e.g. "weapons:2") → option index.</summary>
    public Dictionary<string, int> StartingChoices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Specializations chosen for the Role template's "choose 1" skills, by skill id.</summary>
    public Dictionary<string, string> SkillChoices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Campaign variant of the creation limits; null fields use character-rules.json.</summary>
    public CampaignOverrides Overrides { get; init; } = new();
}

/// <summary>
/// GM-approved variants of the creation parameters (e.g. the "Major Hero" STAT pool of CP:R p.78). Shown on the
/// sheet as a campaign variant, never applied silently.
/// </summary>
public sealed record CampaignOverrides
{
    public int? StatPoints { get; init; }
    public int? StatMin { get; init; }
    public int? StatMax { get; init; }
    public int? SkillPoints { get; init; }
    public int? SkillMax { get; init; }
    public int? GearBudget { get; init; }
    public int? FashionBudget { get; init; }
    public string? Note { get; init; }

    [JsonIgnore]
    public bool Any => StatPoints is not null || StatMin is not null || StatMax is not null || SkillPoints is not null
                       || SkillMax is not null || GearBudget is not null || FashionBudget is not null;
}

// ------------------------------------------------------------ modifiers

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierTarget { Stat, Skill, AllActions }

/// <summary>
/// A temporary or situational effect (drug, cyberware bonus entered by hand, GM ruling). Applied on top of the
/// base values when computing the effective ones; the base is never changed.
/// </summary>
public sealed record StatModifier
{
    public string Id { get; init; } = "";
    public ModifierTarget Target { get; init; }
    /// <summary>STAT key or skill key; empty for <see cref="ModifierTarget.AllActions"/>.</summary>
    public string Key { get; init; } = "";
    public int Value { get; init; }
    public string Source { get; init; } = "";
    public bool Active { get; init; } = true;
}

// ------------------------------------------------------------ inventory

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemState { Carried, Equipped, Stored }

/// <summary>Where an item came from. Trade, Gift and Loot are received without paying (players swap gear between themselves).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemOrigin { Starting, Purchase, Manual, Loot, Trade, Gift }

/// <summary>Which creation budget paid for an item (Complete Package keeps them apart, CP:R p.104–105).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BudgetPool { Balance, CreationGear, CreationFashion }

public sealed record InventoryEntry
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
    public ItemState State { get; init; } = ItemState.Carried;
    /// <summary>Armor worn on this location. Only meaningful when equipped.</summary>
    public ArmorSlot? Worn { get; init; }
    /// <summary>Current SP after ablation; null means as cataloged (undamaged).</summary>
    public int? CurrentSp { get; init; }
    /// <summary>Current HP of a shield; null means full.</summary>
    public int? CurrentHp { get; init; }
    /// <summary>Generic ammunition: caliber and loose rounds owned.</summary>
    public Caliber? Caliber { get; init; }
    public int? Rounds { get; init; }
    public WeaponQuality? Quality { get; init; }
    public WeaponState? Weapon { get; init; }
    /// <summary>Cyberware options held by a worn host (Smart Glasses, Battleglove, Smart Ears...); see <c>cyberware.wearables</c>.</summary>
    public WearableState? Wearable { get; init; }
    public ItemOrigin Origin { get; init; } = ItemOrigin.Manual;
    public BudgetPool PaidFrom { get; init; } = BudgetPool.Balance;
    public int? PricePaid { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Magazine and attachments of a weapon instance.</summary>
public sealed record WeaponState
{
    public int LoadedRounds { get; init; }
    /// <summary>Ammunition item currently loaded (basic, AP, incendiary...).</summary>
    public int? LoadedAmmoItemId { get; init; }
    /// <summary>Inventory instances of the attachments installed in the weapon's slots (CP:R p.95).</summary>
    public List<string> Attachments { get; init; } = [];
}

/// <summary>
/// Options in a wearable host. They are inventory instances, like weapon attachments: worn, not implanted, so they cost
/// no Humanity, and they only work while the host is equipped.
/// </summary>
public sealed record WearableState
{
    public List<string> Options { get; init; } = [];
    /// <summary>Hand a glove is worn on: the options of the arm under it stop working while it is worn.</summary>
    public BodySide? Side { get; init; }
}

// ------------------------------------------------------------ cyberware

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BodySide { Left, Right }

public sealed record CharacterCyberware
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    /// <summary>Foundational piece this option sits in; null for standalone and foundational cyberware.</summary>
    public string? ParentInstanceId { get; init; }
    /// <summary>Eye, ear, arm or leg side, when the piece has one.</summary>
    public BodySide? Side { get; init; }
    /// <summary>Installed as the only piece of cyberware in a meat arm or leg (CP:R p.115–116).</summary>
    public bool MeatLimb { get; init; }
    /// <summary>Humanity actually lost on installation: preset at creation, rolled afterwards (CP:R p.110).</summary>
    public int HumanityLossApplied { get; init; }
    public bool HumanityLossRolled { get; init; }
    /// <summary>Armor implants (Subdermal Armor, Skin Weave) ablate like worn armor; null means undamaged.</summary>
    public int? CurrentSp { get; init; }
    public ItemOrigin Origin { get; init; } = ItemOrigin.Manual;
    public BudgetPool PaidFrom { get; init; } = BudgetPool.Balance;
    public int? PricePaid { get; init; }
    public DateTime? InstalledAt { get; init; }
}

// ------------------------------------------------------------ combat and injuries

public sealed record CombatState
{
    /// <summary>Current Hit Points; null means at maximum (so a higher BODY or WILL raises it too).</summary>
    public int? CurrentHp { get; init; }
    /// <summary>
    /// Death Save Penalty accumulated since the last Stabilization (CP:R p.188), on top of the base penalty from
    /// Critical Injuries. Only advanced by an explicit action, never by time passing.
    /// </summary>
    public int DeathSavePenalty { get; init; }
    public bool Stabilized { get; init; } = true;
    public int LuckSpent { get; init; }
    public List<string> Conditions { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InjuryLocation { Body, Head }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InjuryStatus
{
    /// <summary>Full effect.</summary>
    Active,
    /// <summary>Quick Fix applied: the effect is suspended, but the injury still needs Treatment unless the rules say otherwise.</summary>
    QuickFixed,
    /// <summary>Treated: the injury is gone.</summary>
    Healed,
}

public sealed record CriticalInjuryRecord
{
    public string Id { get; init; } = "";
    /// <summary>Name as in the Critical Injury tables (CP:R p.187–188), or a custom one.</summary>
    public string Name { get; init; } = "";
    public InjuryLocation Location { get; init; }
    public InjuryStatus Status { get; init; } = InjuryStatus.Active;
    /// <summary>Side or detail (left arm, right eye).</summary>
    public string? Detail { get; init; }
    public DateTime ReceivedAt { get; init; }
    /// <summary>Combat event that caused it, when recorded through the combat panel.</summary>
    public long? CombatEventId { get; init; }
    /// <summary>
    /// When the Quick Fix was applied. It suspends the effect for the rest of that day (CP:R p.222); the sheet
    /// shows it and the player returns the injury to Active by hand — time passing never changes it.
    /// </summary>
    public DateTime? QuickFixedAt { get; init; }
    public string? QuickFixNote { get; init; }
    public string? TreatmentNote { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

// ------------------------------------------------------------ humanity

/// <summary>
/// Running totals of Humanity changes; the history lives in the character events. Current Humanity =
/// EMP × 10 − <see cref="Lost"/> + <see cref="Recovered"/>, capped by therapy limits on recovery.
/// </summary>
public sealed record HumanityState
{
    public int Lost { get; init; }
    public int Recovered { get; init; }
}

// ------------------------------------------------------------ lifepath and style

public sealed record LifepathState
{
    public string? CulturalRegion { get; init; }
    /// <summary>Answers to the general Lifepath tables, by table id (personality, valueMost, lifeGoal...).</summary>
    public Dictionary<string, string> Answers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Relationship> Relationships { get; init; } = [];
    public string? Background { get; init; }

    public LifepathState Normalize()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        var relationships = (Relationships ?? [])
            .Where(r => r is not null)
            .Select(r =>
            {
                var id = r.Id;
                if (string.IsNullOrWhiteSpace(id) || !used.Add(id))
                {
                    do id = $"rel{++n}"; while (!used.Add(id));
                }
                return r with { Id = id, Name = r.Name?.Trim() ?? "" };
            })
            .ToList();
        return this with
        {
            Answers = new Dictionary<string, string>(
                (Answers ?? []).Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim()),
                StringComparer.OrdinalIgnoreCase),
            Relationships = relationships,
        };
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelationshipKind { Friend, Enemy, Ally, Lover, Contact }

public sealed record Relationship
{
    public string Id { get; init; } = "";
    public RelationshipKind Kind { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Who they are to you (Lifepath "relationship" or "who" column).</summary>
    public string? Relation { get; init; }
    public string? Description { get; init; }
    public string? History { get; init; }
    /// <summary>Why the relationship exists; for enemies, what caused it (CP:R p.51).</summary>
    public string? Cause { get; init; }
    /// <summary>Enemies: who was wronged.</summary>
    public string? Wronged { get; init; }
    /// <summary>What they can throw at you / their resources and influence.</summary>
    public string? Resources { get; init; }
    /// <summary>Goals or intentions; for enemies, what they will do about it (Sweet Revenge, CP:R p.52).</summary>
    public string? Intent { get; init; }
    /// <summary>Tragic love affairs: how it ended (CP:R p.52).</summary>
    public string? Outcome { get; init; }
    public string? Notes { get; init; }
}

public sealed record StyleState
{
    public string? ClothingStyle { get; init; }
    public string? Hairstyle { get; init; }
    public string? Affectation { get; init; }
    public string? Appearance { get; init; }
    public string? Notes { get; init; }
    /// <summary>Inventory instances shown as the outfit (optional link between narrative and items).</summary>
    public List<string> OutfitItems { get; init; } = [];
}

// ------------------------------------------------------------ housing, money, progression

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus { Paid, Pending, Overdue, NotApplicable }

public sealed record HousingState
{
    public string? HousingId { get; init; }
    public string? Location { get; init; }
    public string? LifestyleId { get; init; }
    /// <summary>Rent agreed in play when it differs from the table (shared rent, a deal with a Fixer).</summary>
    public int? RentOverride { get; init; }
    /// <summary>Last day covered by the payments made. The payment status is derived from it, never stored.</summary>
    public DateOnly? PaidUntil { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Money. After creation everything is paid from <see cref="Balance"/>. During a Complete Package creation the
/// purchases are paid from the two creation budgets (CP:R p.104–105): what was spent is always recomputed from the
/// items' <see cref="BudgetPool"/> and price, and the remainder is settled when the character is finalized.
/// </summary>
public sealed record Wallet
{
    public int Balance { get; init; }
}

public sealed record ProgressState
{
    /// <summary>Unspent Improvement Points.</summary>
    public int Ip { get; init; }
    public int IpEarned { get; init; }
}
