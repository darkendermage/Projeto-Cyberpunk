using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs.Rules;

namespace CyberpunkDb.Application.Characters.Rules;

// martial-arts-rules.json: the forms and their Special Moves (CP:R p.178-180, Interface RED 4 p.76-83, IR5 p.78).
// kind "attack" = one strike in place of the 2 Martial Arts attacks, resolved at the table; "defense" = an answer to
// an incoming attack (Cut the Bullet); "check" = Resolution against dv, the GM applies the effect.
public sealed record MartialArtsDocument
{
    public string About { get; init; } = "";
    public Ruled<string> Resolution { get; init; } = new();
    public List<SpecialMoveRule> SharedMoves { get; init; } = [];
    public List<MartialArtFormRule> Forms { get; init; } = [];
}

public sealed record MartialArtFormRule
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public int Page { get; init; }
    public Text Description { get; init; } = new("", "");
    // NPC-only fighting styles nobody can learn
    public bool? Innate { get; init; }
    public List<SpecialMoveRule> Moves { get; init; } = [];
}

public sealed record SpecialMoveRule
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Text Requirement { get; init; } = new("", "");
    public Text Effect { get; init; } = new("", "");
    public string Kind { get; init; } = "check";
    public int? Dv { get; init; }
    public List<MoveRequirement> Requires { get; init; } = [];
    public List<MoveStrike> Strikes { get; init; } = [];
    public MoveDefense? Defense { get; init; }
}

// stat or skill (level) at least min; form = level in the move's own form
public sealed record MoveRequirement(string? Stat = null, string? Skill = null, int? Form = null, int Min = 0);

// one way to land the strike: modifier on the attack, to the head (like an Aimed Shot), Critical Injury or condition on a hit.
// Versus "brawling": the target resists with Brawling + 1d10 instead of Evasion; NoDamage: the move's effect replaces the damage.
public sealed record MoveStrike(string Id = "strike", int Modifier = 0, bool Head = false, string? Injury = null, string? Condition = null, Text? Label = null,
    string? Versus = null, bool NoDamage = false);

// against: attack modes it answers ("Thrown" = a thrown weapon); wielding "melee" = needs a melee weapon in hand
public sealed record MoveDefense(List<string> Against, string? Wielding = null, string Versus = "attack");

public sealed class MartialArtsRuleBook
{
    private const string Resource = "CyberpunkDb.Application.Characters.Rules.martial-arts-rules.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Lazy<MartialArtsRuleBook> EmbeddedBook = new(() =>
    {
        using var stream = typeof(MartialArtsRuleBook).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"Recurso {Resource} não encontrado.");
        return new MartialArtsRuleBook(JsonSerializer.Deserialize<MartialArtsDocument>(stream, Json)
                                       ?? throw new InvalidOperationException("martial-arts-rules.json vazio."));
    });

    public MartialArtsRuleBook(MartialArtsDocument document) => Document = document;

    public static MartialArtsRuleBook Embedded => EmbeddedBook.Value;

    public MartialArtsDocument Document { get; }

    public MartialArtFormRule? Form(string? name) =>
        name is null ? null : Document.Forms.FirstOrDefault(f => f.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public (MartialArtFormRule Form, SpecialMoveRule Move)? Move(string form, string moveId) =>
        Form(form) is { } f && f.Moves.FirstOrDefault(m => m.Id == moveId) is { } m ? (f, m) : null;

    // stat(key) and skill(id) give the character's numbers; null = unknown, which lets the move through (the GM decides)
    public static bool Meets(SpecialMoveRule move, Func<string, int?> stat, Func<string, int?> skill, int formLevel) =>
        move.Requires.All(r =>
            r.Stat is { } s ? (stat(s) ?? int.MaxValue) >= r.Min
            : r.Skill is { } k ? (skill(k) ?? int.MaxValue) >= r.Min
            : r.Form is { } level ? formLevel >= level
            : true);

    public static string Page(MartialArtFormRule f) => $"{f.Source} p.{f.Page}";
}

public sealed record MartialArtFormDto(string Name, string Source, int Page, string Description, bool Innate, IReadOnlyList<SpecialMoveDto> Moves);

public sealed record SpecialMoveDto(string Id, string Name, string Requirement, string Effect, string Kind, int? Dv);

public static class MartialArtsDtos
{
    public static IReadOnlyList<MartialArtFormDto> Forms(MartialArtsRuleBook book) =>
        book.Document.Forms.Select(f => new MartialArtFormDto(f.Name, f.Source, f.Page, f.Description.ToString(), f.Innate == true, f.Moves.Select(Move).ToList())).ToList();

    public static IReadOnlyList<SpecialMoveDto> Shared(MartialArtsRuleBook book) => book.Document.SharedMoves.Select(Move).ToList();

    private static SpecialMoveDto Move(SpecialMoveRule m) => new(m.Id, m.Name, m.Requirement.ToString(), m.Effect.ToString(), m.Kind, m.Dv);
}
