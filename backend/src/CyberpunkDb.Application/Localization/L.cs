using System.Text.Json;

namespace CyberpunkDb.Application.Localization;

// Message language for the current request (PT or EN), set by the API from Accept-Language or ?lang=. Without a
// request (tests, startup) everything stays in Portuguese. Rule files and catalog data are always PT; D()
// translates them when they end up in a message.
public static class L
{
    private static readonly AsyncLocal<bool?> Portuguese = new();

    // frontend/src/i18n/data-en.json, embedded at build time
    private static readonly Lazy<IReadOnlyDictionary<string, string>> DataEn = new(() =>
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("i18n.data-en.json");
        return stream is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    });

    public static bool IsPortuguese => Portuguese.Value ?? true;

    public static string T(string pt, string en) => IsPortuguese ? pt : en;

    // English when the dictionary has it
    public static string D(string pt) => IsPortuguese || !DataEn.Value.TryGetValue(pt, out var en) ? pt : en;

    // PT label in PT-BR, the book's English name otherwise
    public static string SkillName(string name, string? namePt) => IsPortuguese ? namePt ?? name : name;

    // Console window and startup logs follow the Windows display language.
    public static string ForSystem(string pt, string en) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;

    // for the rest of the async flow (one HTTP request)
    public static void Use(string? languageTag) =>
        Portuguese.Value = languageTag?.TrimStart().StartsWith("pt", StringComparison.OrdinalIgnoreCase) == true;
}
