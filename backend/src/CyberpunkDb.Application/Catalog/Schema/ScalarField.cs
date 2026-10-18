using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Schema;

/// <summary>A single-valued attribute backed by an expression such as <c>w =&gt; w.Rof</c>.</summary>
public sealed class ScalarField<T, TProp> : FieldDefinition<T> where T : Item
{
    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
    private static readonly bool AllowsNull = !typeof(TProp).IsValueType || Nullable.GetUnderlyingType(typeof(TProp)) is not null;

    private readonly Expression<Func<T, TProp>> _selector;
    private readonly Func<T, TProp> _getter;
    private readonly PropertyInfo? _property;

    public ScalarField(string key, string label, FieldKind kind, Expression<Func<T, TProp>> selector) : base(key, label, kind)
    {
        _selector = selector;
        _getter = selector.Compile();

        // Writable only when the selector is a direct property access on the entity.
        if (selector.Body is MemberExpression { Member: PropertyInfo { CanWrite: true } prop } member &&
            member.Expression == selector.Parameters[0])
            _property = prop;
        else
            IsEditable = false;

        if (Underlying.IsEnum)
            _options = EnumLabels.For(Underlying).Select(o => new OptionDto(o.Value, o.Label)).ToList();
    }

    private readonly IReadOnlyList<OptionDto>? _options;
    public override IReadOnlyList<OptionDto>? Options => _options;

    public Expression<Func<T, TProp>> Selector => _selector;

    public override IReadOnlyList<string> Operators => Kind switch
    {
        FieldKind.Number when !AllowsNull => FilterOps.Numeric.Except([FilterOps.IsNull, FilterOps.NotNull]).ToArray(),
        FieldKind.Number => FilterOps.Numeric,
        FieldKind.Enum => FilterOps.Enum,
        FieldKind.Boolean => FilterOps.Boolean,
        _ => FilterOps.Text,
    };

    public override object? GetValue(T entity)
    {
        var value = _getter(entity);
        return value is Enum e ? e.ToString() : value;
    }

    public override string? GetDisplayText(T entity) => _getter(entity) switch
    {
        null => null,
        Enum e => EnumLabels.Label(e),
        bool b => b ? "Sim" : "Não",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var v => v.ToString(),
    };

    public override void SetValue(T entity, JsonElement? value)
    {
        if (_property is null) throw Invalid($"O campo '{Label}' é somente leitura.");

        var converted = FieldValueConverter.Convert(value, typeof(TProp), Key);
        if (converted is null && !AllowsNull)
            throw Invalid($"'{Label}' é obrigatório.");
        if (converted is not null && Kind == FieldKind.Number)
        {
            var number = System.Convert.ToDouble(converted, CultureInfo.InvariantCulture);
            if ((MinValue is { } min && number < min) || (MaxValue is { } max && number > max))
                throw Invalid($"'{Label}' deve estar entre {MinValue} e {MaxValue}.");
        }
        if (converted is string s && Kind == FieldKind.Text && s.Length > 500)
            throw Invalid($"'{Label}' aceita no máximo 500 caracteres.");

        _property.SetValue(entity, converted);
    }

    public override Expression<Func<T, bool>> BuildPredicate(FilterCriterion c)
    {
        EnsureOperator(c.Op);
        var body = _selector.Body;
        var param = _selector.Parameters[0];

        Expression predicate = c.Op switch
        {
            FilterOps.IsNull => Expression.Equal(body, Expression.Constant(null, typeof(TProp))),
            FilterOps.NotNull => Expression.NotEqual(body, Expression.Constant(null, typeof(TProp))),
            FilterOps.Contains => BuildContains(body, c),
            FilterOps.In => BuildIn(body, ReadValues(c)),
            FilterOps.NotIn => Expression.Not(BuildIn(body, ReadValues(c))),
            // Enum equality goes through IN so it works regardless of the enum's storage conversion.
            FilterOps.Eq when Kind == FieldKind.Enum => BuildIn(body, [ReadSingle(c.Value)]),
            FilterOps.Neq when Kind == FieldKind.Enum => Expression.Not(BuildIn(body, [ReadSingle(c.Value)])),
            FilterOps.Eq => Expression.Equal(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Neq => Expression.NotEqual(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Gt => Expression.GreaterThan(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Gte => Expression.GreaterThanOrEqual(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Lt => Expression.LessThan(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Lte => Expression.LessThanOrEqual(body, ValueHolder<TProp>.Expr(ReadSingle(c.Value))),
            FilterOps.Between => BuildBetween(body, c),
            _ => throw Invalid($"Operador '{c.Op}' não suportado."),
        };

        return Expression.Lambda<Func<T, bool>>(predicate, param);
    }

    public override IOrderedQueryable<T> ApplySort(IQueryable<T> query, bool descending)
    {
        if (!IsSortable) return base.ApplySort(query, descending);
        if (!AllowsNull)
            return descending ? query.OrderByDescending(_selector) : query.OrderBy(_selector);

        // Nulls always last, whichever the direction.
        var isNull = Expression.Lambda<Func<T, bool>>(
            Expression.Equal(_selector.Body, Expression.Constant(null, typeof(TProp))), _selector.Parameters);
        var ordered = query.OrderBy(isNull);
        return descending ? ordered.ThenByDescending(_selector) : ordered.ThenBy(_selector);
    }

    public override async Task<IReadOnlyList<FacetValue>> FacetAsync(IQueryable<T> query, CancellationToken ct)
    {
        if (!IsFacet) return [];
        var rows = await query.GroupBy(_selector).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return ToFacets(rows.Where(r => r.Key is not null).Select(r => (r.Key!, r.Count)));
    }

    public override async Task<(double? Min, double? Max)> RangeAsync(IQueryable<T> query, CancellationToken ct)
    {
        if (Kind != FieldKind.Number) return (null, null);
        var asDouble = Expression.Lambda<Func<T, double?>>(Expression.Convert(_selector.Body, typeof(double?)), _selector.Parameters);
        var projected = query.Select(asDouble).Where(v => v != null);
        return (await projected.MinAsync(ct), await projected.MaxAsync(ct));
    }

    public override async Task<IReadOnlyList<double>?> DistinctValuesAsync(IQueryable<T> query, int limit, CancellationToken ct)
    {
        if (Kind != FieldKind.Number) return null;
        var asDouble = Expression.Lambda<Func<T, double?>>(Expression.Convert(_selector.Body, typeof(double?)), _selector.Parameters);
        var values = await query.Select(asDouble).Where(v => v != null).Distinct().OrderBy(v => v).Take(limit + 1).ToListAsync(ct);
        return values.Count > limit ? null : values.Select(v => v!.Value).ToList();
    }

    internal static IReadOnlyList<FacetValue> ToFacets(IEnumerable<(TProp Key, int Count)> rows)
    {
        var list = rows.ToList();
        if (Underlying.IsEnum)
        {
            // Keep the declaration order of the enum, which is the natural reading order.
            var counts = list.ToDictionary(r => r.Key!.ToString()!, r => r.Count);
            return EnumLabels.For(Underlying)
                .Where(o => counts.ContainsKey(o.Value))
                .Select(o => new FacetValue(o.Value, o.Label, counts[o.Value]))
                .ToList();
        }
        return list
            .Select(r => new FacetValue(Convert.ToString(r.Key, CultureInfo.InvariantCulture)!, Convert.ToString(r.Key, CultureInfo.InvariantCulture)!, r.Count))
            .OrderBy(f => f.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private TProp ReadSingle(JsonElement? value)
    {
        var converted = FieldValueConverter.Convert(value, typeof(TProp), Key)
            ?? throw Invalid($"Informe um valor para '{Label}'.");
        return (TProp)converted;
    }

    private List<TProp> ReadValues(FilterCriterion c)
    {
        var raw = c.Values ?? (c.Value is { } v ? [v] : []);
        var list = raw.Where(e => !FieldValueConverter.IsEmpty(e)).Select(e => ReadSingle(e)).Distinct().ToList();
        if (list.Count == 0) throw Invalid($"Selecione ao menos um valor para '{Label}'.");
        if (list.Count > 100) throw Invalid($"Valores demais para '{Label}'.");
        return list;
    }

    private Expression BuildIn(Expression body, List<TProp> values)
    {
        var contains = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TProp));
        return Expression.Call(contains, ValueHolder<List<TProp>>.Expr(values), body);
    }

    private Expression BuildBetween(Expression body, FilterCriterion c)
    {
        var hasMin = !FieldValueConverter.IsEmpty(c.Min);
        var hasMax = !FieldValueConverter.IsEmpty(c.Max);
        if (!hasMin && !hasMax) throw Invalid($"Informe mínimo e/ou máximo para '{Label}'.");

        Expression? result = null;
        if (hasMin) result = Expression.GreaterThanOrEqual(body, ValueHolder<TProp>.Expr(ReadSingle(c.Min)));
        if (hasMax)
        {
            var upper = Expression.LessThanOrEqual(body, ValueHolder<TProp>.Expr(ReadSingle(c.Max)));
            result = result is null ? upper : Expression.AndAlso(result, upper);
        }
        return result!;
    }

    private Expression BuildContains(Expression body, FilterCriterion c)
    {
        if (typeof(TProp) != typeof(string)) throw Invalid($"'{Label}' não aceita busca textual.");
        var term = (FieldValueConverter.Convert(c.Value, typeof(string), Key) as string)?.ToLowerInvariant()
                   ?? throw Invalid($"Informe um texto para '{Label}'.");
        var toLower = Expression.Call(body, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
        var contains = Expression.Call(toLower, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
            ValueHolder<string>.Expr(term));
        return Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, typeof(string))), contains);
    }
}
