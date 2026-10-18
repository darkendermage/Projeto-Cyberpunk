using System.Globalization;
using System.Text.Json;
using CyberpunkDb.Application.Common;

namespace CyberpunkDb.Application.Catalog.Schema;

/// <summary>Converts JSON values coming from filters, forms and imports into CLR values of a field.</summary>
public static class FieldValueConverter
{
    public static bool IsEmpty(JsonElement? element) =>
        element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        (element.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.Value.GetString()));

    /// <summary>Returns null for empty input. Throws <see cref="RequestValidationException"/> on invalid input.</summary>
    public static object? Convert(JsonElement? element, Type targetType, string fieldKey)
    {
        if (IsEmpty(element)) return null;
        var el = element!.Value;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        object? result = type switch
        {
            _ when type == typeof(string) => el.ValueKind == JsonValueKind.String ? el.GetString()!.Trim() : el.GetRawText(),
            _ when type == typeof(int) => ToInt(el),
            _ when type == typeof(decimal) => ToDecimal(el),
            _ when type == typeof(double) => (double?)ToDecimal(el),
            _ when type == typeof(bool) => ToBool(el),
            _ when type.IsEnum => ToEnum(el, type),
            _ => null,
        };

        return result ?? throw RequestValidationException.For(fieldKey, $"Valor inválido: {Describe(el)}.");
    }

    public static T? ConvertTo<T>(JsonElement? element, string fieldKey) =>
        Convert(element, typeof(T), fieldKey) is { } v ? (T)v : default;

    private static int? ToInt(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String &&
            int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static decimal? ToDecimal(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String &&
            decimal.TryParse(el.GetString()?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static bool? ToBool(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => el.GetString()!.Trim().ToLowerInvariant() switch
        {
            "true" or "sim" or "yes" or "1" => true,
            "false" or "nao" or "não" or "no" or "0" => false,
            _ => null,
        },
        _ => null,
    };

    private static object? ToEnum(JsonElement el, Type enumType)
    {
        if (el.ValueKind != JsonValueKind.String) return null;
        var raw = el.GetString()!.Trim();
        // Only accept declared names or labels; Enum.TryParse would also accept arbitrary integers.
        foreach (var (value, label) in EnumLabels.For(enumType))
        {
            if (string.Equals(value, raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, raw, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse(enumType, value);
        }
        return null;
    }

    private static string Describe(JsonElement el)
    {
        var raw = el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.GetRawText();
        return raw.Length > 40 ? $"'{raw[..40]}…'" : $"'{raw}'";
    }
}
