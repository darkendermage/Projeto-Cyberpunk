using System.Text.RegularExpressions;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>Dice rolls for the operations the server resolves (template rows, STATs, Humanity Loss, Death Saves).</summary>
public interface IDiceRoller
{
    /// <summary>1 to <paramref name="sides"/>.</summary>
    int Roll(int sides);
}

public sealed class RandomDiceRoller : IDiceRoller
{
    public int Roll(int sides) => Random.Shared.Next(1, sides + 1);
}

public static partial class Dice
{
    /// <summary>
    /// Rolls an expression like "2d6", "4d6" or "1d6/2" (halved, rounded up, as the Humanity Loss table prints it,
    /// CP:R p.111). Returns null when the expression is not understood.
    /// </summary>
    public static int? Roll(string? expression, IDiceRoller roller)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var m = Expression().Match(expression.Replace(" ", ""));
        if (!m.Success) return null;
        var count = int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var total = Enumerable.Range(0, count).Sum(_ => roller.Roll(sides));
        if (m.Groups[3].Success) total = (int)Math.Ceiling(total / (double)int.Parse(m.Groups[3].Value));
        return total;
    }

    /// <summary>Lowest and highest result of an expression, for previews before rolling.</summary>
    public static (int Min, int Max)? Range(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var m = Expression().Match(expression.Replace(" ", ""));
        if (!m.Success) return null;
        var count = int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var divisor = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 1;
        return ((int)Math.Ceiling(count / (double)divisor), (int)Math.Ceiling(count * sides / (double)divisor));
    }

    [GeneratedRegex(@"^(\d+)d(\d+)(?:/(\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex Expression();
}
