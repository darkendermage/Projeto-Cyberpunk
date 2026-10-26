using CyberpunkDb.Application.Localization;
namespace CyberpunkDb.Application.Npcs.Generation;

internal static class Weighted
{
    /// <summary>Picks one element with probability proportional to its weight (non-positive weights never win unless all are).</summary>
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, double> weight, Random rng)
    {
        if (items.Count == 0) throw new InvalidOperationException(L.T("Nada para escolher.", "Nothing to pick."));
        var weights = items.Select(i => Math.Max(0, weight(i))).ToArray();
        var total = weights.Sum();
        if (total <= 0) return items[rng.Next(items.Count)];
        var roll = rng.NextDouble() * total;
        for (var i = 0; i < items.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return items[i];
        }
        return items[^1];
    }

    public static int Between(Random rng, int min, int max) => min >= max ? min : rng.Next(min, max + 1);

    public static double Between(Random rng, double min, double max) => min + rng.NextDouble() * (max - min);
}
