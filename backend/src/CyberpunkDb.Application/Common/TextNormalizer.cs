using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CyberpunkDb.Application.Common;

public static partial class TextNormalizer
{
    /// <summary>Lowercase, strip diacritics and collapse whitespace so "Escopeta Pesada" matches "escopeta".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        }
        return Whitespace().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    public static IReadOnlyList<string> Tokenize(string? query) =>
        Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(8).ToList();

    public static string Slugify(string value)
    {
        var normalized = Normalize(value);
        var slug = NonSlug().Replace(normalized, "-").Trim('-');
        return slug.Length > 120 ? slug[..120].Trim('-') : slug;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
