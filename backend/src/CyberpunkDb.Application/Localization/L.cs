using System.Text.Json;

namespace CyberpunkDb.Application.Localization;

/// <summary>
/// Language of the messages built for the current request: Portuguese or English. The API sets it per request from
/// Accept-Language (or <c>?lang=</c>); with no request at all (unit tests, startup) the texts stay in Portuguese, the
/// language they were written in. Rule files and catalog data stay in Portuguese: the frontend translates them for
/// display, and <see cref="D"/> does the same here when such a text goes into a message.
/// </summary>
public static class L
{
    private static readonly AsyncLocal<bool?> Portuguese = new();

    /// <summary>The frontend's data dictionary (frontend/src/i18n/data-en.json), embedded at build time.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> DataEn = new(() =>
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("i18n.data-en.json");
        return stream is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    });

    public static bool IsPortuguese => Portuguese.Value ?? true;

    /// <summary>Picks the text for the current language.</summary>
    public static string T(string pt, string en) => IsPortuguese ? pt : en;

    /// <summary>A text from the rule files or the catalog, in English when the dictionary has it.</summary>
    public static string D(string pt) => IsPortuguese || !DataEn.Value.TryGetValue(pt, out var en) ? pt : en;

    /// <summary>Skill names: the Portuguese label in PT-BR, the book's English name otherwise.</summary>
    public static string SkillName(string name, string? namePt) => IsPortuguese ? namePt ?? name : name;

    /// <summary>
    /// Texts outside any request (the desktop console window, startup logs) follow the Windows display language.
    /// </summary>
    public static string ForSystem(string pt, string en) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;

    /// <summary>Sets the language for the rest of the current async flow (one HTTP request).</summary>
    public static void Use(string? languageTag) =>
        Portuguese.Value = languageTag?.TrimStart().StartsWith("pt", StringComparison.OrdinalIgnoreCase) == true;
}
