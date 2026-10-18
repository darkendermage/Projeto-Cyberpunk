using System.Linq.Expressions;
using System.Text.Json;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Catalog.Schema;

public enum FieldKind { Number, Enum, Boolean, Text, LongText, Dice, Set }

/// <summary>
/// Describes one attribute of a category: how to read it, write it, filter on it, sort by it and facet it.
/// The same definition drives the API schema, the query builder and the editor, so they cannot drift apart.
/// </summary>
public abstract class FieldDefinition<T> where T : Item
{
    protected FieldDefinition(string key, string label, FieldKind kind)
    {
        Key = key;
        Label = label;
        Kind = kind;
        IsFilterable = kind is FieldKind.Number or FieldKind.Enum or FieldKind.Boolean or FieldKind.Set;
        IsFacet = kind is FieldKind.Enum or FieldKind.Set;
        IsSortable = kind is FieldKind.Number;
    }

    public string Key { get; }
    public string Label { get; }
    public FieldKind Kind { get; }
    public string? Unit { get; private set; }
    public string? Group { get; private set; }
    public string? Description { get; private set; }
    public bool IsFilterable { get; private set; }
    public bool IsSortable { get; private set; }
    public bool IsFacet { get; private set; }
    public bool IsEditable { get; protected set; } = true;
    public bool IsRequired { get; private set; }
    public bool IsBase { get; private set; }
    public string? DefaultOperator { get; private set; }
    public IReadOnlyList<string>? AppliesToValues { get; private set; }
    public double? MinValue { get; private set; }
    public double? MaxValue { get; private set; }

    public abstract IReadOnlyList<string> Operators { get; }
    public virtual IReadOnlyList<OptionDto>? Options => null;

    // ---- fluent configuration ----
    public FieldDefinition<T> WithUnit(string unit) { Unit = unit; return this; }
    public FieldDefinition<T> InGroup(string group) { Group = group; return this; }
    public FieldDefinition<T> Describe(string description) { Description = description; return this; }
    public FieldDefinition<T> Filterable(bool value = true) { IsFilterable = value; return this; }
    public FieldDefinition<T> Sortable(bool value = true) { IsSortable = value; return this; }
    public FieldDefinition<T> Facet(bool value = true) { IsFacet = value; return this; }
    public FieldDefinition<T> ReadOnly() { IsEditable = false; return this; }
    public FieldDefinition<T> Required() { IsRequired = true; return this; }
    public FieldDefinition<T> DefaultOp(string op) { DefaultOperator = op; return this; }
    public FieldDefinition<T> Range(double min, double max) { MinValue = min; MaxValue = max; return this; }
    internal FieldDefinition<T> AsBase() { IsBase = true; return this; }

    /// <summary>Restricts the field to items whose subtype is one of the given values (e.g. SP only for armor, not clothing).</summary>
    public FieldDefinition<T> AppliesTo(params Enum[] subtypes)
    {
        AppliesToValues = subtypes.Select(s => s.ToString()).ToList();
        return this;
    }

    // ---- behaviour ----
    /// <summary>JSON-friendly value (enums as their member name).</summary>
    public abstract object? GetValue(T entity);
    /// <summary>Human-readable value (enums as their pt-BR label).</summary>
    public abstract string? GetDisplayText(T entity);
    public abstract void SetValue(T entity, JsonElement? value);
    public abstract Expression<Func<T, bool>> BuildPredicate(FilterCriterion criterion);

    public virtual IOrderedQueryable<T> ApplySort(IQueryable<T> query, bool descending) =>
        throw RequestValidationException.For("sort", $"O campo '{Key}' não pode ser usado para ordenar.");

    public virtual Task<IReadOnlyList<FacetValue>> FacetAsync(IQueryable<T> query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FacetValue>>([]);

    public virtual Task<(double? Min, double? Max)> RangeAsync(IQueryable<T> query, CancellationToken ct) =>
        Task.FromResult<(double?, double?)>((null, null));

    /// <summary>Sorted distinct values of a numeric field, or null when there are more than <paramref name="limit"/>.</summary>
    public virtual Task<IReadOnlyList<double>?> DistinctValuesAsync(IQueryable<T> query, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<double>?>(null);

    public bool AppliesToSubtype(string? subtype) =>
        AppliesToValues is null || subtype is null || AppliesToValues.Contains(subtype);

    public FieldDescriptorDto ToDescriptor(double? min = null, double? max = null, IReadOnlyList<double>? values = null) => new()
    {
        Key = Key,
        Label = Label,
        Kind = Kind.ToString().ToLowerInvariant(),
        Unit = Unit,
        Group = Group,
        Description = Description,
        Filterable = IsFilterable,
        Sortable = IsSortable,
        Facet = IsFacet,
        Editable = IsEditable,
        Required = IsRequired,
        Base = IsBase,
        DefaultOp = DefaultOperator ?? Operators.FirstOrDefault(),
        Operators = IsFilterable ? Operators : [],
        AppliesTo = AppliesToValues,
        Options = Options,
        Min = min,
        Max = max,
        Values = values,
    };

    protected RequestValidationException Invalid(string message) => RequestValidationException.For(Key, message);

    protected void EnsureOperator(string op)
    {
        if (!IsFilterable) throw Invalid($"O campo '{Label}' não aceita filtros.");
        if (!Operators.Contains(op)) throw Invalid($"Operador '{op}' não é válido para '{Label}'. Use: {string.Join(", ", Operators)}.");
    }
}

/// <summary>Wraps a filter value so EF Core sends it as a SQL parameter instead of inlining a literal.</summary>
internal sealed class ValueHolder<TValue>(TValue value)
{
    public TValue Value { get; } = value;

    public static Expression Expr(TValue value) =>
        Expression.Property(Expression.Constant(new ValueHolder<TValue>(value)), nameof(Value));
}
