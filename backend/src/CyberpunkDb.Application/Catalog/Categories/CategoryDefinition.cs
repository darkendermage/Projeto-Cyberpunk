using System.Linq.Expressions;
using System.Text.Json;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Categories;

/// <summary>
/// Base for every category. A subclass only declares its fields (and optionally includes, relations
/// and cross-field rules); searching, faceting, sorting, mapping and validation are shared.
/// </summary>
public abstract class CategoryDefinition<T> : ICategoryDefinition where T : Item, new()
{
    public const int MaxPageSize = 100;

    private IReadOnlyList<FieldDefinition<T>>? _fields;
    private Dictionary<string, FieldDefinition<T>>? _byKey;

    public abstract ItemCategory Category { get; }
    public abstract string Key { get; }
    public abstract string Label { get; }
    public virtual string? Sublabel => null;
    public abstract string Description { get; }
    public abstract string Icon { get; }
    public Type EntityType => typeof(T);

    /// <summary>Key of the enum field that classifies items inside the category (weapon class, armor type...).</summary>
    protected abstract string SubtypeField { get; }
    /// <summary>Fields shown on cards, in priority order; the UI keeps the first ones that apply.</summary>
    protected abstract string[] CardFields { get; }
    /// <summary>Attribute columns of the table view, between "Fabricante" and "Custo".</summary>
    protected virtual string[] TableFields => CardFields;
    protected virtual string DefaultSort => "name";
    protected abstract IEnumerable<FieldDefinition<T>> DefineFields();
    protected virtual IQueryable<T> IncludeDetails(IQueryable<T> query) => query;
    protected virtual IEnumerable<(string Field, string Message)> ValidateItem(T item) => [];
    protected virtual Task<IReadOnlyList<RelationGroupDto>> GetCategoryRelationsAsync(IAppDbContext db, T item, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RelationGroupDto>>([]);

    public IReadOnlyList<FieldDefinition<T>> Fields => _fields ??= BaseFields().Concat(DefineFields()).ToList();

    public FieldDefinition<T>? FindField(string key)
    {
        _byKey ??= Fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        return _byKey.GetValueOrDefault(key);
    }

    private FieldDefinition<T> Subtype => FindField(SubtypeField)!;

    // ---------------------------------------------------------------- field helpers

    protected static FieldDefinition<T> Number<TP>(string key, string label, Expression<Func<T, TP>> selector) =>
        new ScalarField<T, TP>(key, label, FieldKind.Number, selector);

    protected static FieldDefinition<T> Choice<TP>(string key, string label, Expression<Func<T, TP>> selector) =>
        new ScalarField<T, TP>(key, label, FieldKind.Enum, selector);

    protected static FieldDefinition<T> Flag(string key, string label, Expression<Func<T, bool>> selector) =>
        new ScalarField<T, bool>(key, label, FieldKind.Boolean, selector);

    protected static FieldDefinition<T> Text(string key, string label, Expression<Func<T, string?>> selector) =>
        new ScalarField<T, string?>(key, label, FieldKind.Text, selector);

    protected static FieldDefinition<T> LongText(string key, string label, Expression<Func<T, string?>> selector) =>
        new ScalarField<T, string?>(key, label, FieldKind.LongText, selector);

    protected static FieldDefinition<T> Dice(string key, string label, Expression<Func<T, string?>> selector) =>
        new ScalarField<T, string?>(key, label, FieldKind.Dice, selector);

    protected static FieldDefinition<T> Set<TElem, TValue>(string key, string label,
        Expression<Func<T, IEnumerable<TElem>>> collection, Expression<Func<TElem, TValue>> value, Action<T, List<TValue>> setter) =>
        new SetField<T, TElem, TValue>(key, label, collection, value, setter);

    /// <summary>Updates a child collection in place so EF Core tracks removals/additions instead of key conflicts.</summary>
    public static void Sync<TElem, TValue>(List<TElem> list, List<TValue> values, Func<TElem, TValue> key, Func<TValue, TElem> create)
    {
        list.RemoveAll(e => !values.Contains(key(e)));
        foreach (var value in values)
            if (!list.Any(e => EqualityComparer<TValue>.Default.Equals(key(e), value)))
                list.Add(create(value));
    }

    private static IEnumerable<FieldDefinition<T>> BaseFields()
    {
        yield return Text("name", "Nome", i => i.Name).Sortable().AsBase();
        yield return Number("price", "Preço", i => i.PriceEb).WithUnit("eb").InGroup("Preço").Range(0, 10_000_000).AsBase();
        yield return Choice("priceCategory", "Categoria de preço", i => i.PriceCategory).InGroup("Preço").AsBase();
        yield return Choice("manufacturer", "Fabricante", i => i.Manufacturer != null ? i.Manufacturer.Name : null).AsBase();
        yield return Set("tags", "Propriedades", i => i.Tags, t => t.Value,
                (i, values) => Sync(i.Tags, values, t => t.Value, v => new ItemTag { Value = v }))
            .DefaultOp(FilterOps.All).AsBase();
        yield return Choice("source", "Fonte / Livro", i => i.SourceBook != null ? i.SourceBook.Name : null).InGroup("Fonte").AsBase();
        yield return Choice("ruleSystem", "Sistema de regras", i => i.RuleSystem).InGroup("Fonte").AsBase();
        yield return Choice("dataOrigin", "Origem dos dados", i => i.DataOrigin).InGroup("Fonte").AsBase();
    }

    // ---------------------------------------------------------------- queries

    private static IQueryable<T> WithBaseIncludes(IQueryable<T> query) =>
        query.Include(i => i.Manufacturer).Include(i => i.SourceBook).Include(i => i.Tags);

    public async Task<SearchResult> SearchAsync(IAppDbContext db, SearchRequest request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, request.Page);
        var criteria = CompileCriteria(request.Filters);

        var searched = ApplyTextSearch(db.Set<T>().AsNoTracking(), request.Q);
        var filtered = ApplyCriteria(searched, criteria, exceptField: null);

        var total = await filtered.CountAsync(ct);
        var ordered = ApplySort(IncludeDetails(WithBaseIncludes(filtered)), request.Sort, request.Dir);
        var entities = await ordered.Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync(ct);

        var facets = new Dictionary<string, IReadOnlyList<FacetValue>>();
        if (request.IncludeFacets)
        {
            // Each facet is counted with every criterion except its own, so users see how many items
            // they would get by changing that one filter (standard faceted-search behaviour).
            foreach (var field in Fields.Where(f => f.IsFacet && f.IsFilterable))
                facets[field.Key] = await field.FacetAsync(ApplyCriteria(searched, criteria, field.Key), ct);
        }

        return new SearchResult(
            entities.Select(e => ToDto(e)).ToList(),
            total, page, pageSize,
            (int)Math.Ceiling(total / (double)pageSize),
            facets);
    }

    /// <summary>Validates every criterion up front and turns it into a predicate.</summary>
    public IReadOnlyList<(string Field, Expression<Func<T, bool>> Predicate)> CompileCriteria(IEnumerable<FilterCriterion>? filters)
    {
        var list = filters?.ToList() ?? [];
        if (list.Count > 40) throw RequestValidationException.For("filters", "Critérios demais (máximo 40).");

        var errors = new Dictionary<string, string[]>();
        var compiled = new List<(string, Expression<Func<T, bool>>)>();
        foreach (var criterion in list)
        {
            var field = FindField(criterion.Field);
            if (field is null)
            {
                errors[criterion.Field] = [$"O campo '{criterion.Field}' não existe em {Label}."];
                continue;
            }
            try
            {
                compiled.Add((field.Key, field.BuildPredicate(criterion)));
            }
            catch (RequestValidationException ex)
            {
                foreach (var (k, v) in ex.Errors) errors[k] = v;
            }
        }
        if (errors.Count > 0) throw new RequestValidationException(errors);
        return compiled;
    }

    public static IQueryable<T> ApplyCriteria(IQueryable<T> query,
        IEnumerable<(string Field, Expression<Func<T, bool>> Predicate)> criteria, string? exceptField) =>
        criteria.Where(c => !string.Equals(c.Field, exceptField, StringComparison.OrdinalIgnoreCase))
            .Aggregate(query, (q, c) => q.Where(c.Predicate));

    public static IQueryable<T> ApplyTextSearch(IQueryable<T> query, string? text)
    {
        foreach (var token in TextNormalizer.Tokenize(text))
            query = query.Where(i => i.SearchText.Contains(token));
        return query;
    }

    public IOrderedQueryable<T> ApplySort(IQueryable<T> query, string? sort, string? dir)
    {
        var descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        var field = FindField(string.IsNullOrWhiteSpace(sort) ? DefaultSort : sort)
                    ?? throw RequestValidationException.For("sort", $"Ordenação '{sort}' desconhecida.");
        if (!field.IsSortable)
            throw RequestValidationException.For("sort", $"O campo '{field.Label}' não pode ser usado para ordenar.");

        // Name and id as tie-breakers keep paging stable.
        return field.Key == "name"
            ? (descending ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name)).ThenBy(i => i.Id)
            : field.ApplySort(query, descending).ThenBy(i => i.Name).ThenBy(i => i.Id);
    }

    public Task<int> CountAsync(IAppDbContext db, CancellationToken ct) => db.Set<T>().CountAsync(ct);

    public async Task<CategorySchemaDto> GetSchemaAsync(IAppDbContext db, CancellationToken ct)
    {
        var query = db.Set<T>().AsNoTracking();
        var descriptors = new List<FieldDescriptorDto>();
        foreach (var field in Fields)
        {
            var (min, max) = field.IsFilterable ? await field.RangeAsync(query, ct) : (null, null);
            var values = field.IsFilterable ? await field.DistinctValuesAsync(query, 40, ct) : null;
            descriptors.Add(field.ToDescriptor(min, max, values));
        }

        var categoryFields = Fields.Where(f => !f.IsBase && f.IsFilterable).Select(f => f.Key).ToList();
        var filterOrder = new List<string> { SubtypeField, "manufacturer" }
            .Concat(categoryFields.Where(k => k != SubtypeField))
            .Concat(["tags", "price", "priceCategory", "source", "ruleSystem", "dataOrigin"])
            .Distinct()
            .ToList();

        return new CategorySchemaDto
        {
            Key = Key,
            Category = Category.ToString(),
            Label = Label,
            Sublabel = Sublabel,
            Description = Description,
            Icon = Icon,
            SubtypeField = SubtypeField,
            CardFields = CardFields,
            TableFields = TableFields,
            FilterOrder = filterOrder,
            DefaultSort = DefaultSort,
            SortOptions = Fields.Where(f => f.IsSortable).Select(f => new SortOptionDto(f.Key, f.Label)).ToList(),
            Fields = descriptors,
            ItemCount = await query.CountAsync(ct),
        };
    }

    public async Task<Item?> LoadAsync(IAppDbContext db, int id, bool tracking, CancellationToken ct)
    {
        var query = IncludeDetails(WithBaseIncludes(db.Set<T>()));
        if (!tracking) query = query.AsNoTracking();
        return await query.AsSplitQuery().FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IReadOnlyList<ItemDto>> LoadManyAsync(IAppDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var entities = await IncludeDetails(WithBaseIncludes(db.Set<T>().AsNoTracking()))
            .Where(i => ids.Contains(i.Id)).AsSplitQuery().ToListAsync(ct);
        return entities.Select(e => ToDto(e)).ToList();
    }

    public async Task<IReadOnlyList<RelationGroupDto>> GetRelationsAsync(IAppDbContext db, Item item, CancellationToken ct)
    {
        var groups = new List<RelationGroupDto>(await GetCategoryRelationsAsync(db, (T)item, ct));
        if (item.ManufacturerId is { } manufacturerId)
        {
            var same = await db.Items.AsNoTracking()
                .Where(i => i.ManufacturerId == manufacturerId && i.Id != item.Id)
                .OrderBy(i => i.Category).ThenBy(i => i.Name).Take(12)
                .Select(i => new ItemSummaryDto(i.Id, i.Slug, i.Name, i.Category.ToString(), i.PriceEb, null))
                .ToListAsync(ct);
            if (same.Count > 0)
                groups.Add(new RelationGroupDto("sameManufacturer", $"Outros itens {item.Manufacturer?.Name}", same));
        }
        return groups;
    }

    // ---------------------------------------------------------------- mapping

    public ItemDto ToDto(Item item)
    {
        var entity = (T)item;
        var subtype = Subtype;
        return new ItemDto
        {
            Id = entity.Id,
            Slug = entity.Slug,
            Name = entity.Name,
            Category = Key,
            Subtype = subtype.GetValue(entity) is string value ? new OptionDto(value, subtype.GetDisplayText(entity)!) : null,
            Description = entity.Description,
            Notes = entity.Notes,
            Price = entity.PriceEb,
            PriceNote = entity.PriceNote,
            PriceCategory = entity.PriceCategory?.ToString(),
            Manufacturer = entity.Manufacturer is { } m ? new NamedRef(m.Slug, m.Name) : null,
            Source = entity.SourceBook is { } s ? new NamedRef(s.Slug, s.Name) : null,
            Page = entity.Page,
            ImageUrl = entity.ImageUrl,
            RuleSystem = entity.RuleSystem.ToString(),
            DataOrigin = entity.DataOrigin.ToString(),
            Tags = entity.Tags.Select(t => t.Value).OrderBy(t => t).ToList(),
            Attributes = Fields.Where(f => !f.IsBase).ToDictionary(f => f.Key, f => f.GetValue(entity)),
            Extra = ParseExtra(entity.ExtraAttributesJson),
        };
    }

    private static JsonElement? ParseExtra(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    public string BuildSearchText(Item item)
    {
        var entity = (T)item;
        var parts = new List<string?>
        {
            entity.Name, entity.Description, entity.Notes, entity.Manufacturer?.Name, entity.SourceBook?.Name, Label,
        };
        parts.AddRange(entity.Tags.Select(t => t.Value));
        parts.AddRange(Fields.Where(f => !f.IsBase && f.Kind is FieldKind.Enum or FieldKind.Text or FieldKind.LongText or FieldKind.Set)
            .Select(f => f.GetDisplayText(entity)));
        return TextNormalizer.Normalize(string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))));
    }


    // ---------------------------------------------------------------- writes

    public Item CreateEntity() => new T();

    public Dictionary<string, string[]> ApplyAttributes(Item item, IReadOnlyDictionary<string, JsonElement> attributes, bool isCreate)
    {
        var entity = (T)item;
        var errors = new Dictionary<string, string[]>();

        foreach (var (key, value) in attributes)
        {
            var field = FindField(key);
            if (field is null || field.IsBase)
            {
                errors[$"attributes.{key}"] = [$"Atributo '{key}' não existe em {Label}."];
                continue;
            }
            if (!field.IsEditable) continue;
            try
            {
                field.SetValue(entity, value);
            }
            catch (RequestValidationException ex)
            {
                foreach (var (k, v) in ex.Errors) errors[$"attributes.{k}"] = v;
            }
        }

        if (isCreate)
        {
            foreach (var field in Fields.Where(f => f.IsRequired && !attributes.ContainsKey(f.Key)))
                errors[$"attributes.{field.Key}"] = [$"'{field.Label}' é obrigatório."];
        }

        if (errors.Count == 0)
        {
            // Generic rule: attributes restricted to some subtypes must stay empty for the others,
            // e.g. clothing cannot carry Stopping Power and a shotgun cannot have a humanity cost.
            var subtype = Subtype.GetValue(entity) as string;
            foreach (var field in Fields.Where(f => !f.AppliesToSubtype(subtype)))
            {
                if (HasValue(field.GetValue(entity)))
                    errors[$"attributes.{field.Key}"] = [$"'{field.Label}' não se aplica a {Subtype.GetDisplayText(entity)}."];
            }
            foreach (var (field, message) in ValidateItem(entity))
                errors[$"attributes.{field}"] = [message];
        }

        return errors;
    }

    private static bool HasValue(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrWhiteSpace(s),
        System.Collections.ICollection c => c.Count > 0,
        _ => true,
    };
}
