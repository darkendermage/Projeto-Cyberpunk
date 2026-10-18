using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Schema;

/// <summary>
/// A multi-valued attribute backed by a child collection, e.g. weapon calibers or item tags.
/// Supports "all" (item has every value), "in" (item has any value) and "nin" (item has none).
/// </summary>
public sealed class SetField<T, TElem, TValue> : FieldDefinition<T> where T : Item
{
    private static readonly MethodInfo AnyWithPredicate = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(TElem));

    private static readonly MethodInfo ContainsMethod = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(TValue));

    private readonly Expression<Func<T, IEnumerable<TElem>>> _collection;
    private readonly Expression<Func<TElem, TValue>> _value;
    private readonly Func<T, IEnumerable<TElem>> _collectionGetter;
    private readonly Func<TElem, TValue> _valueGetter;
    private readonly Action<T, List<TValue>> _setter;
    private readonly IReadOnlyList<OptionDto>? _options;

    public SetField(string key, string label,
        Expression<Func<T, IEnumerable<TElem>>> collection,
        Expression<Func<TElem, TValue>> value,
        Action<T, List<TValue>> setter) : base(key, label, FieldKind.Set)
    {
        _collection = collection;
        _value = value;
        _collectionGetter = collection.Compile();
        _valueGetter = value.Compile();
        _setter = setter;
        if (typeof(TValue).IsEnum)
            _options = EnumLabels.For(typeof(TValue)).Select(o => new OptionDto(o.Value, o.Label)).ToList();
        Sortable(false);
    }

    public override IReadOnlyList<OptionDto>? Options => _options;
    public override IReadOnlyList<string> Operators => FilterOps.Set;

    public override object? GetValue(T entity) =>
        _collectionGetter(entity).Select(_valueGetter).Select(v => v is Enum e ? e.ToString() : (object?)v).ToList();

    public override string? GetDisplayText(T entity)
    {
        var parts = _collectionGetter(entity).Select(_valueGetter)
            .Select(v => v is Enum e ? EnumLabels.Label(e) : Convert.ToString(v, CultureInfo.InvariantCulture))
            .ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public override void SetValue(T entity, JsonElement? value)
    {
        if (FieldValueConverter.IsEmpty(value)) { _setter(entity, []); return; }
        if (value!.Value.ValueKind != JsonValueKind.Array) throw Invalid($"'{Label}' deve ser uma lista.");
        var list = value.Value.EnumerateArray().Select(ReadSingle).Distinct().ToList();
        if (list.Count > 50) throw Invalid($"'{Label}' aceita no máximo 50 valores.");
        _setter(entity, list);
    }

    public override Expression<Func<T, bool>> BuildPredicate(FilterCriterion c)
    {
        EnsureOperator(c.Op);
        var raw = c.Values ?? (c.Value is { } v ? [v] : []);
        var values = raw.Where(e => !FieldValueConverter.IsEmpty(e)).Select(e => ReadSingle(e)).Distinct().ToList();
        if (values.Count == 0) throw Invalid($"Selecione ao menos um valor para '{Label}'.");

        var param = _collection.Parameters[0];
        Expression body = c.Op switch
        {
            FilterOps.In => AnyOf(values),
            FilterOps.NotIn => Expression.Not(AnyOf(values)),
            FilterOps.All => values.Select(val => AnyOf([val])).Aggregate(Expression.AndAlso),
            _ => throw Invalid($"Operador '{c.Op}' não suportado."),
        };
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    public override async Task<IReadOnlyList<FacetValue>> FacetAsync(IQueryable<T> query, CancellationToken ct)
    {
        if (!IsFacet) return [];
        var rows = await query.SelectMany(_collection).GroupBy(_value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return ScalarField<T, TValue>.ToFacets(rows.Where(r => r.Key is not null).Select(r => (r.Key, r.Count)));
    }

    /// <summary>x =&gt; x.Collection.Any(e =&gt; values.Contains(e.Value))</summary>
    private Expression AnyOf(List<TValue> values)
    {
        var contains = Expression.Call(ContainsMethod, ValueHolder<List<TValue>>.Expr(values), _value.Body);
        var elementPredicate = Expression.Lambda<Func<TElem, bool>>(contains, _value.Parameters);
        return Expression.Call(AnyWithPredicate, _collection.Body, elementPredicate);
    }

    private TValue ReadSingle(JsonElement element) =>
        FieldValueConverter.Convert(element, typeof(TValue), Key) is TValue v
            ? v
            : throw Invalid($"Valor vazio em '{Label}'.");
}
