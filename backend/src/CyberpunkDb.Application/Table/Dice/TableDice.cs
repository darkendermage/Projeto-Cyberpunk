using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table.Dice;

/// <summary>
/// How the table reads a roll: Plain adds everything; Check is a Skill Check (one d10, which can go critical); Damage
/// adds the dice and watches for a Critical Injury.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RollKind { Plain, Check, Damage }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CriticalResult { Success, Failure }

/// <summary>One term of a formula: <see cref="Count"/>d<see cref="Sides"/>, or a constant when <see cref="Sides"/> is 0.</summary>
public sealed record DiceTerm(int Sign, int Count, int Sides, int Constant)
{
    public bool IsDice => Sides > 0;
    public override string ToString() => IsDice ? $"{Count}d{Sides}" : Constant.ToString();
}

/// <summary>A formula like "1d10+12", "3d6" or "2d6+1d6-2": dice and constants joined by + and -.</summary>
public sealed partial record DiceFormula(IReadOnlyList<DiceTerm> Terms)
{
    public const int MaxDice = 100;
    public const int MaxSides = 1000;
    public const int MaxTerms = 20;

    public static DiceFormula Parse(string? text)
    {
        var compact = (text ?? "").Replace(" ", "").ToLowerInvariant();
        if (compact.Length is 0 or > 80 || !Whole().IsMatch(compact)) throw Invalid();
        var terms = Term().Matches(compact).Select(m =>
        {
            var sign = m.Groups["sign"].Value == "-" ? -1 : 1;
            if (!m.Groups["d"].Success) return new DiceTerm(sign, 0, 0, Number(m.Groups["n"].Value, 1000));
            var count = m.Groups["n"].Success && m.Groups["n"].Value.Length > 0 ? Number(m.Groups["n"].Value, MaxDice) : 1;
            var sides = Number(m.Groups["s"].Value, MaxSides);
            if (count < 1 || sides < 2) throw Invalid();
            return new DiceTerm(sign, count, sides, 0);
        }).ToList();
        if (terms.Count > MaxTerms || terms.Where(t => t.IsDice).Sum(t => t.Count) > MaxDice) throw Invalid();
        return new DiceFormula(terms);
    }

    public override string ToString() => string.Concat(Terms.Select((t, i) => (t.Sign < 0 ? "-" : i == 0 ? "" : "+") + t));

    private static int Number(string text, int max) =>
        int.TryParse(text, out var n) && n <= max ? n : throw Invalid();

    private static RequestValidationException Invalid() =>
        RequestValidationException.For("formula", L.T(
            "Fórmula inválida. Use dados e números com + e -, como 1d10+12 ou 2d6+1d6-2 (até 100 dados).",
            "Invalid formula. Use dice and numbers with + and -, like 1d10+12 or 2d6+1d6-2 (up to 100 dice)."));

    [GeneratedRegex(@"^[+-]?(\d*d\d+|\d+)([+-](\d*d\d+|\d+))*$")]
    private static partial Regex Whole();

    [GeneratedRegex(@"(?<sign>[+-]?)(?:(?<n>\d*)(?<d>d)(?<s>\d+)|(?<n>\d+))")]
    private static partial Regex Term();
}

/// <summary>A rolled term: the dice that came up (and the critical's extra d10), and what the term adds to the total.</summary>
public sealed record RolledTerm(string Expression, int Sign, IReadOnlyList<int> Dice, int? Extra, int Value);

/// <summary>
/// A server roll. <see cref="Luck"/>: LUCK points spent on it, already in <see cref="Total"/> (+1 each, CP:R p.130);
/// <see cref="RerollOf"/>: the roll this one replaces (the table's 1-LUCK reroll, a house rule).
/// </summary>
public sealed record RollResult(
    string Formula, RollKind Kind, IReadOnlyList<RolledTerm> Terms, int Total, CriticalResult? Critical, bool CriticalInjury,
    int Luck = 0, long? RerollOf = null);

/// <summary>The server rolls every die of the virtual table, so nobody's dice can be doctored.</summary>
public static class TableRoller
{
    public static RollResult Roll(string formula, RollKind kind, IDiceRoller dice)
    {
        var parsed = DiceFormula.Parse(formula);
        if (kind == RollKind.Check && parsed.Terms.Count(t => t.IsDice) != 1 || kind == RollKind.Check && parsed.Terms.Single(t => t.IsDice) is not { Count: 1, Sides: 10 })
            throw RequestValidationException.For("formula", L.T(
                "Um teste de perícia rola um único d10 mais modificadores (ex.: 1d10+12).",
                "A Skill Check rolls a single d10 plus modifiers (e.g. 1d10+12)."));

        CriticalResult? critical = null;
        var terms = parsed.Terms.Select(t =>
        {
            if (!t.IsDice) return new RolledTerm(t.ToString(), t.Sign, [], null, t.Sign * t.Constant);
            var rolled = Enumerable.Range(0, t.Count).Select(_ => dice.Roll(t.Sides)).ToList();
            int? extra = null;
            // CP:R p.130: a natural 10 rolls one more d10 and adds it; a natural 1 rolls one more and subtracts it.
            // The extra die never chains.
            if (kind == RollKind.Check && rolled[0] is 10 or 1)
            {
                critical = rolled[0] == 10 ? CriticalResult.Success : CriticalResult.Failure;
                extra = rolled[0] == 10 ? dice.Roll(10) : -dice.Roll(10);
            }
            return new RolledTerm(t.ToString(), t.Sign, rolled, extra, t.Sign * (rolled.Sum() + (extra ?? 0)));
        }).ToList();

        // CP:R p.187: two or more 6s on the damage dice cause a Critical Injury (5 bonus damage straight to HP).
        var criticalInjury = kind == RollKind.Damage && parsed.Terms.Zip(terms)
            .Where(p => p.First is { IsDice: true, Sides: 6, Sign: > 0 })
            .SelectMany(p => p.Second.Dice).Count(d => d == 6) >= 2;
        return new RollResult(parsed.ToString(), kind, terms, terms.Sum(t => t.Value), critical, criticalInjury);
    }
}
