using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CyberpunkDb.Application.Common;

/// <summary>Reads the pt-BR labels declared with [Display] on domain enums.</summary>
public static class EnumLabels
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<(string Value, string Label)>> Cache = new();

    public static IReadOnlyList<(string Value, string Label)> For(Type enumType) =>
        Cache.GetOrAdd(enumType, t => t
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f.Name, f.GetCustomAttribute<DisplayAttribute>()?.Name ?? f.Name))
            .ToList());

    public static string Label(Enum value)
    {
        var name = value.ToString();
        foreach (var (v, label) in For(value.GetType()))
            if (v == name) return label;
        return name;
    }
}
