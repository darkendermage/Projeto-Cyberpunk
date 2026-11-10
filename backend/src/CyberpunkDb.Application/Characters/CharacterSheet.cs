using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters;

// What gets saved for a PC. Only source data: derived values are computed by CharacterCalculator.
// Skill keys follow the NPC ones ("handgun", "language:Streetslang").
public sealed record CharacterSheet
{
    public CharacterIdentity Identity { get; init; } = new();
    // First one is the current Role (CP:R p.142)
    public List<CharacterRole> Roles { get; init; } = [];
    public CreationState Creation { get; init; } = new();
    // Base values, LUCK included. Modifiers never touch these.
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StatModifier> Modifiers { get; init; } = [];
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<InventoryEntry> Inventory { get; init; } = [];
    public List<CharacterCyberware> Cyberware { get; init; } = [];
    public CombatState Combat { get; init; } = new();
    public List<CriticalInjuryRecord> Injuries { get; init; } = [];
    public HumanityState Humanity { get; init; } = new();
    public LifepathState Lifepath { get; init; } = new();
    public Dictionary<string, Dictionary<string, string>> RoleLifepath { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public StyleState Style { get; init; } = new();
    public HousingState Housing { get; init; } = new();
    public Wallet Wallet { get; init; } = new();
    public ProgressState Progress { get; init; } = new();
    // Per-Role module data, kept as raw JSON so new modules don't break saved sheets.
    public Dictionary<string, System.Text.Json.JsonElement> RoleData { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Notes { get; init; }

    [JsonIgnore]
    public CharacterRole? PrimaryRole => Roles.Count > 0 ? Roles[0] : null;

    public int Stat(string key) => Stats.TryGetValue(key, out var v) ? v : 0;

    public CharacterSheet Normalize() => this with
    {
        Identity = (Identity ?? new()).Normalize(),
        Roles = (Roles ?? []).Select(r => r with { Split = new Dictionary<string, int>(r.Split ?? [], StringComparer.OrdinalIgnoreCase) }).ToList(),
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

// identity and roles

public sealed record CharacterIdentity
{
    public string Name { get; init; } = "";
    public string? Handle { get; init; }
    public string? Pronouns { get; init; }
    public int? Age { get; init; }
    public string? Concept { get; init; }
    // Free Language at 4 from the Cultural Origin (CP:R p.45)
    public string? NativeLanguage { get; init; }
    // Local Expert (Your Home) specialization
    public string Home { get; init; } = "Watson";
    public int Reputation { get; init; }
    // null = centered, no zoom
    public PortraitFocus? PortraitFocus { get; init; }

    public CharacterIdentity Normalize() => this with
    {
        PortraitFocus = PortraitFocus?.Clamp(),
        Name = Name?.Trim() ?? "",
        Handle = Blank(Handle),
        Pronouns = Blank(Pronouns),
        Concept = Blank(Concept),
        NativeLanguage = Blank(NativeLanguage),
        Home = string.IsNullOrWhiteSpace(Home) ? "Watson" : Home.Trim(),
    };

    internal static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// X/Y in % of the picture, zoom 1-3
public sealed record PortraitFocus(double X = 50, double Y = 50, double Zoom = 1)
{
    public PortraitFocus Clamp() => new(
        double.IsFinite(X) ? Math.Clamp(X, 0, 100) : 50,
        double.IsFinite(Y) ? Math.Clamp(Y, 0, 100) : 50,
        double.IsFinite(Zoom) ? Math.Clamp(Zoom, 1, 3) : 1);
}

// Multiclassing adds roles after creation (CP:R p.142). Ability names come from the rules, not from here.
public sealed record CharacterRole
{
    public string Role { get; init; } = "";
    public int Rank { get; init; }
    public int IpInvested { get; init; }
    // Tech Maker specialties (p.147) / Solo Combat Awareness (p.146), keys from character-rules.json
    public Dictionary<string, int> Split { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? AcquiredAt { get; init; }
    public string? Notes { get; init; }
}

// creation

// Kept so the creation method can be re-checked later
public sealed record CreationState
{
    public CreationMethod Method { get; init; }
    public bool Finalized { get; init; }
    public DateTime? FinalizedAt { get; init; }
    // Streetrat 1d10 row
    public int? TemplateRow { get; init; }
    // Edgerunner rolls per STAT
    public Dictionary<string, int> StatRolls { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    // "section:group" (e.g. "weapons:2") -> option index
    public Dictionary<string, int> StartingChoices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SkillChoices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    // null fields fall back to character-rules.json
    public CampaignOverrides Overrides { get; init; } = new();
}

// GM-approved tweaks, e.g. the "Major Hero" pool (CP:R p.78). Shown on the sheet, never applied silently.
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

// modifiers

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierTarget { Stat, Skill, AllActions }

// Drugs, hand-entered cyberware bonuses, GM rulings... applied on top of the base values.
public sealed record StatModifier
{
    public string Id { get; init; } = "";
    public ModifierTarget Target { get; init; }
    // empty for AllActions
    public string Key { get; init; } = "";
    public int Value { get; init; }
    public string Source { get; init; } = "";
    public bool Active { get; init; } = true;
}

// inventory

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemState { Carried, Equipped, Stored }

// Trade/Gift/Loot don't cost money (players swapping gear)
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemOrigin { Starting, Purchase, Manual, Loot, Trade, Gift }

// Complete Package keeps two budgets apart (CP:R p.104-105)
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BudgetPool { Balance, CreationGear, CreationFashion }

public sealed record InventoryEntry
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
    public ItemState State { get; init; } = ItemState.Carried;
    public ArmorSlot? Worn { get; init; }
    // null = undamaged
    public int? CurrentSp { get; init; }
    // null = full
    public int? CurrentHp { get; init; }
    public Caliber? Caliber { get; init; }
    public int? Rounds { get; init; }
    public WeaponQuality? Quality { get; init; }
    public WeaponState? Weapon { get; init; }
    // Smart Glasses, Battleglove, Smart Ears... see cyberware.wearables
    public WearableState? Wearable { get; init; }
    public ItemOrigin Origin { get; init; } = ItemOrigin.Manual;
    public BudgetPool PaidFrom { get; init; } = BudgetPool.Balance;
    public int? PricePaid { get; init; }
    public string? Notes { get; init; }
}

public sealed record WeaponState
{
    public int LoadedRounds { get; init; }
    public int? LoadedAmmoItemId { get; init; }
    // CP:R p.95
    public List<string> Attachments { get; init; } = [];
    // Poor Quality weapon jammed on a natural 1 (CP:R p.344), or a bolt action waiting for its Action
    public bool Jammed { get; init; }
    // mode switched on with an Action (weapon-rules.json), null = the first one
    public string? Mode { get; init; }
    // extra magazines declared in weapon-rules.json: rounds and the ammo item loaded in each
    public Dictionary<string, int> Magazines { get; init; } = [];
    public Dictionary<string, int> MagazineAmmo { get; init; } = [];
    public bool Destroyed { get; init; }
}

// Worn, not implanted: no Humanity cost, and they only work while the host is equipped.
public sealed record WearableState
{
    public List<string> Options { get; init; } = [];
    // A glove disables the options of the arm under it
    public BodySide? Side { get; init; }
}

// cyberware

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BodySide { Left, Right }

public sealed record CharacterCyberware
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    public string? ParentInstanceId { get; init; }
    public BodySide? Side { get; init; }
    // only cyberware in a meat limb (CP:R p.115-116)
    public bool MeatLimb { get; init; }
    // preset at creation, rolled afterwards (CP:R p.110)
    public int HumanityLossApplied { get; init; }
    public bool HumanityLossRolled { get; init; }
    // Subdermal Armor / Skin Weave ablate like worn armor. null = undamaged
    public int? CurrentSp { get; init; }
    public ItemOrigin Origin { get; init; } = ItemOrigin.Manual;
    public BudgetPool PaidFrom { get; init; } = BudgetPool.Balance;
    public int? PricePaid { get; init; }
    public DateTime? InstalledAt { get; init; }
}

// combat and injuries

public sealed record CombatState
{
    // null = max, so raising BODY/WILL raises it too
    public int? CurrentHp { get; init; }
    // Since the last Stabilization (CP:R p.188). Only an explicit action moves it.
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
    Active,
    // still needs Treatment unless the rules say otherwise
    QuickFixed,
    Healed,
}

public sealed record CriticalInjuryRecord
{
    public string Id { get; init; } = "";
    // as in the Critical Injury tables (CP:R p.187-188), or custom
    public string Name { get; init; } = "";
    public InjuryLocation Location { get; init; }
    public InjuryStatus Status { get; init; } = InjuryStatus.Active;
    public string? Detail { get; init; }
    public DateTime ReceivedAt { get; init; }
    public long? CombatEventId { get; init; }
    // Quick Fix suspends the effect for the rest of the day (CP:R p.222). The player sets it back to Active by hand.
    public DateTime? QuickFixedAt { get; init; }
    public string? QuickFixNote { get; init; }
    public string? TreatmentNote { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

// humanity

// Current = EMP x 10 - Lost + Recovered (therapy caps recovery). History is in the character events.
public sealed record HumanityState
{
    public int Lost { get; init; }
    public int Recovered { get; init; }
}

// lifepath and style

public sealed record LifepathState
{
    public string? CulturalRegion { get; init; }
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
    public string? Relation { get; init; }
    public string? Description { get; init; }
    public string? History { get; init; }
    // for enemies: what caused it (CP:R p.51)
    public string? Cause { get; init; }
    public string? Wronged { get; init; }
    public string? Resources { get; init; }
    // Sweet Revenge for enemies (CP:R p.52)
    public string? Intent { get; init; }
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
    public List<string> OutfitItems { get; init; } = [];
}

// housing, money, progression

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus { Paid, Pending, Overdue, NotApplicable }

public sealed record HousingState
{
    public string? HousingId { get; init; }
    public string? Location { get; init; }
    public string? LifestyleId { get; init; }
    // when it differs from the table (shared rent, deal with a Fixer)
    public int? RentOverride { get; init; }
    // payment status is derived from this
    public DateOnly? PaidUntil { get; init; }
    public string? Notes { get; init; }
}

// After creation everything comes out of Balance. During a Complete Package creation the two budgets are
// recomputed from each item's BudgetPool (CP:R p.104-105) and settled on finalize.
public sealed record Wallet
{
    public int Balance { get; init; }
}

public sealed record ProgressState
{
    public int Ip { get; init; }
    public int IpEarned { get; init; }
}
