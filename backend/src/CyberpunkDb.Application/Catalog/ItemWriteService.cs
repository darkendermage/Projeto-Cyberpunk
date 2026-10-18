using System.Globalization;
using System.Text;
using System.Text.Json;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Categories;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog;

/// <summary>Write side of the catalog: create, update, delete, import and export.</summary>
public sealed class ItemWriteService(IAppDbContext db, CategoryRegistry registry)
{
    public async Task<ItemDto> CreateAsync(ItemUpsertDto dto, CancellationToken ct)
    {
        var def = registry.Get(dto.Category);
        var item = def.CreateEntity();
        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? TextNormalizer.Slugify(dto.Name) : dto.Slug.Trim();
        if (await db.Items.AnyAsync(i => i.Slug == slug, ct))
            throw RequestValidationException.For("slug", $"Já existe um item com o identificador '{slug}'.");

        await ApplyAsync(def, item, dto, isCreate: true, defaultOrigin: DataOrigin.Custom, ct);
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return def.ToDto(item);
    }

    public async Task<ItemDto> UpdateAsync(int id, ItemUpsertDto dto, CancellationToken ct)
    {
        var item = await LoadTrackedAsync(id, ct);
        var def = registry.For(item);
        if (!string.IsNullOrWhiteSpace(dto.Category) && registry.Get(dto.Category) != def)
            throw RequestValidationException.For("category", "Não é possível mudar a categoria de um item existente.");

        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? item.Slug : dto.Slug.Trim();
        if (slug != item.Slug && await db.Items.AnyAsync(i => i.Slug == slug && i.Id != id, ct))
            throw RequestValidationException.For("slug", $"Já existe um item com o identificador '{slug}'.");

        await ApplyAsync(def, item, dto, isCreate: false, defaultOrigin: item.DataOrigin, ct);
        await db.SaveChangesAsync(ct);
        return def.ToDto(item);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException($"Item {id} não encontrado.");
        // Cyberware options that point to this item lose the link instead of blocking the delete.
        await db.Set<CyberwareItem>().Where(c => c.RequiresItemId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RequiresItemId, (int?)null), ct);
        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Upserts items by slug. Valid rows are saved even if others fail; errors are reported per row.</summary>
    public async Task<ImportResult> ImportAsync(IReadOnlyList<ItemUpsertDto> rows, CancellationToken ct)
    {
        int created = 0, updated = 0;
        var errors = new List<ImportError>();
        var deferredReferences = new List<(CyberwareItem Item, string Reference, int Index)>();

        for (var index = 0; index < rows.Count; index++)
        {
            var dto = rows[index];
            try
            {
                var def = registry.Get(dto.Category);
                var slug = string.IsNullOrWhiteSpace(dto.Slug) ? TextNormalizer.Slugify(dto.Name) : dto.Slug.Trim();
                dto.Slug = slug;

                var existingId = await db.Items.Where(i => i.Slug == slug).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);
                var item = existingId is { } id ? await LoadTrackedAsync(id, ct) : def.CreateEntity();
                if (existingId is not null && registry.For(item) != def)
                    throw RequestValidationException.For("category", $"O item '{slug}' já existe em outra categoria.");

                // References to other items may point to rows later in the file, so they are resolved at the end.
                var reference = TakeReference(dto);
                await ApplyAsync(def, item, dto, existingId is null, DataOrigin.Custom, ct);
                if (reference is not null && item is CyberwareItem cyber) deferredReferences.Add((cyber, reference, index));

                if (existingId is null) { db.Items.Add(item); created++; } else updated++;
                await db.SaveChangesAsync(ct);
            }
            catch (RequestValidationException ex)
            {
                errors.Add(new ImportError(index, dto.Slug, dto.Name, ex.Errors));
                DetachPending();
            }
            catch (NotFoundException ex)
            {
                errors.Add(new ImportError(index, dto.Slug, dto.Name, new Dictionary<string, string[]> { ["category"] = [ex.Message] }));
                DetachPending();
            }
        }

        foreach (var (item, reference, index) in deferredReferences)
        {
            var target = await FindCyberwareAsync(reference, ct);
            if (target is null)
                errors.Add(new ImportError(index, item.Slug, item.Name, new Dictionary<string, string[]>
                {
                    [$"attributes.{CyberwareCategory.RequiresKey}"] = [$"Peça base '{reference}' não encontrada."],
                }));
            else
                await db.Set<CyberwareItem>().Where(c => c.Slug == item.Slug)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.RequiresItemId, (int?)target.Id), ct);
        }

        return new ImportResult(created, updated, errors.Count, errors);
    }

    public async Task<IReadOnlyList<ItemUpsertDto>> ExportAsync(string? categoryKey, CancellationToken ct)
    {
        var defs = string.IsNullOrWhiteSpace(categoryKey) ? registry.All : [registry.Get(categoryKey)];
        var result = new List<ItemUpsertDto>();
        foreach (var def in defs)
        {
            foreach (var item in await def.LoadAllAsync(db, ct))
            {
                var attributes = def.ExportAttributes(item);
                if (item is CyberwareItem { RequiresItem: { } required })
                    attributes[CyberwareCategory.RequiresKey] = JsonSerializer.SerializeToElement(required.Slug);

                result.Add(new ItemUpsertDto
                {
                    Slug = item.Slug,
                    Name = item.Name,
                    Category = def.Key,
                    Description = item.Description,
                    Notes = item.Notes,
                    Price = item.PriceEb,
                    PriceNote = item.PriceNote,
                    PriceCategory = item.PriceCategory?.ToString(),
                    Manufacturer = item.Manufacturer?.Name,
                    Source = item.SourceBook?.Name,
                    Page = item.Page,
                    ImageUrl = item.ImageUrl,
                    RuleSystem = item.RuleSystem.ToString(),
                    DataOrigin = item.DataOrigin.ToString(),
                    Tags = item.Tags.Select(t => t.Value).OrderBy(t => t).ToList(),
                    Attributes = attributes,
                    Extra = string.IsNullOrWhiteSpace(item.ExtraAttributesJson)
                        ? null
                        : JsonDocument.Parse(item.ExtraAttributesJson).RootElement.Clone(),
                });
            }
        }
        return result;
    }

    /// <summary>CSV with base columns plus the category attributes as readable text (enum labels, "Sim"/"Não").</summary>
    public async Task<string> ExportCsvAsync(string categoryKey, CancellationToken ct)
    {
        var def = registry.Get(categoryKey);
        var items = await def.LoadAllAsync(db, ct);
        var columns = def.ExportColumns;

        var sb = new StringBuilder();
        var header = new[] { "slug", "nome", "preco_eb", "fabricante", "fonte", "sistema", "origem", "tags", "descricao" }
            .Concat(columns.Select(c => c.Label));
        sb.AppendLine(string.Join(';', header.Select(Csv)));

        foreach (var item in items)
        {
            var row = def.ToDisplayRow(item);
            var values = new[]
            {
                item.Slug, item.Name, item.PriceEb?.ToString(CultureInfo.InvariantCulture), item.Manufacturer?.Name,
                item.SourceBook?.Name, EnumLabels.Label(item.RuleSystem), EnumLabels.Label(item.DataOrigin),
                string.Join(", ", item.Tags.Select(t => t.Value)), item.Description,
            }.Concat(columns.Select(c => row[c.Key]));
            sb.AppendLine(string.Join(';', values.Select(Csv)));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Item> LoadTrackedAsync(int id, CancellationToken ct)
    {
        var category = await db.Items.Where(i => i.Id == id).Select(i => (ItemCategory?)i.Category).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException($"Item {id} não encontrado.");
        return await registry.Get(category).LoadAsync(db, id, tracking: true, ct)
               ?? throw new NotFoundException($"Item {id} não encontrado.");
    }

    private async Task ApplyAsync(ICategoryDefinition def, Item item, ItemUpsertDto dto, bool isCreate,
        DataOrigin defaultOrigin, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];

        var name = dto.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) Error("name", "Nome é obrigatório.");
        else if (name.Length > 150) Error("name", "Nome aceita no máximo 150 caracteres.");
        if (dto.Description?.Length > 4000) Error("description", "Descrição aceita no máximo 4000 caracteres.");
        if (dto.Notes?.Length > 4000) Error("notes", "Notas aceitam no máximo 4000 caracteres.");
        if (dto.Price is < 0) Error("price", "Preço não pode ser negativo.");
        if (dto.Page is < 1 or > 2000) Error("page", "Página inválida.");
        if (dto.ImageUrl is { Length: > 0 } url && !IsAllowedImageUrl(url)) Error("imageUrl", "Use uma URL http(s) ou um caminho começando com '/'.");

        var priceCategory = ParseEnum<PriceCategory>(dto.PriceCategory, "priceCategory", errors);
        var ruleSystem = ParseEnum<RuleSystem>(dto.RuleSystem, "ruleSystem", errors) ?? item.RuleSystem;
        var origin = ParseEnum<DataOrigin>(dto.DataOrigin, "dataOrigin", errors) ?? defaultOrigin;

        var tags = (dto.Tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tags.Count > 30 || tags.Any(t => t.Length > 40)) Error("tags", "Máximo de 30 propriedades com até 40 caracteres cada.");

        string? extraJson = null;
        if (dto.Extra is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } extra)
        {
            if (extra.ValueKind != JsonValueKind.Object) Error("extra", "Atributos extras devem ser um objeto JSON.");
            else extraJson = extra.GetRawText();
        }

        var attributes = new Dictionary<string, JsonElement>(dto.Attributes ?? [], StringComparer.OrdinalIgnoreCase);
        if (item is CyberwareItem cyberware && attributes.Remove(CyberwareCategory.RequiresKey, out var requires))
        {
            var reference = FieldValueConverter.IsEmpty(requires) ? null : requires.ToString();
            var target = reference is null ? null : await FindCyberwareAsync(reference, ct);
            if (reference is not null && target is null) Error($"attributes.{CyberwareCategory.RequiresKey}", $"Peça base '{reference}' não encontrada.");
            else if (target is not null && target.Id == item.Id && item.Id != 0) Error($"attributes.{CyberwareCategory.RequiresKey}", "Um item não pode requerer a si mesmo.");
            else { cyberware.RequiresItemId = target?.Id; cyberware.RequiresItem = target; }
        }

        foreach (var (key, value) in def.ApplyAttributes(item, attributes, isCreate)) errors[key] = value;
        if (errors.Count > 0) throw new RequestValidationException(errors);

        item.Slug = string.IsNullOrWhiteSpace(dto.Slug) ? (isCreate ? TextNormalizer.Slugify(name) : item.Slug) : dto.Slug.Trim();
        item.Name = name;
        item.Description = NullIfEmpty(dto.Description);
        item.Notes = NullIfEmpty(dto.Notes);
        item.PriceEb = dto.Price;
        item.PriceNote = NullIfEmpty(dto.PriceNote);
        item.PriceCategory = priceCategory;
        item.Page = dto.Page;
        item.ImageUrl = NullIfEmpty(dto.ImageUrl);
        item.RuleSystem = ruleSystem;
        item.DataOrigin = origin;
        item.ExtraAttributesJson = extraJson;
        item.Manufacturer = await FindOrCreateManufacturerAsync(dto.Manufacturer, ct);
        item.ManufacturerId = item.Manufacturer?.Id;
        item.SourceBook = await FindOrCreateSourceAsync(dto.Source, ruleSystem, ct);
        item.SourceBookId = item.SourceBook?.Id;
        CategoryDefinition<Weapon>.Sync(item.Tags, tags, t => t.Value, v => new ItemTag { Value = v });
    }

    private static string? TakeReference(ItemUpsertDto dto)
    {
        if (dto.Attributes is null) return null;
        var key = dto.Attributes.Keys.FirstOrDefault(k => string.Equals(k, CyberwareCategory.RequiresKey, StringComparison.OrdinalIgnoreCase));
        if (key is null) return null;
        var value = dto.Attributes[key];
        dto.Attributes.Remove(key);
        return FieldValueConverter.IsEmpty(value) ? null : value.ToString();
    }

    private async Task<CyberwareItem?> FindCyberwareAsync(string reference, CancellationToken ct)
    {
        var slug = TextNormalizer.Slugify(reference);
        return await db.Set<CyberwareItem>().FirstOrDefaultAsync(c => c.Slug == reference || c.Slug == slug || c.Name == reference, ct);
    }

    private async Task<Manufacturer?> FindOrCreateManufacturerAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var slug = TextNormalizer.Slugify(name);
        var existing = db.Manufacturers.Local.FirstOrDefault(m => m.Slug == slug)
                       ?? await db.Manufacturers.FirstOrDefaultAsync(m => m.Slug == slug, ct);
        if (existing is not null) return existing;
        var created = new Manufacturer { Slug = slug, Name = name.Trim() };
        db.Manufacturers.Add(created);
        return created;
    }

    private async Task<SourceBook?> FindOrCreateSourceAsync(string? name, RuleSystem system, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var slug = TextNormalizer.Slugify(name);
        var existing = db.SourceBooks.Local.FirstOrDefault(s => s.Slug == slug)
                       ?? await db.SourceBooks.FirstOrDefaultAsync(s => s.Slug == slug, ct);
        if (existing is not null) return existing;
        var created = new SourceBook { Slug = slug, Name = name.Trim(), RuleSystem = system };
        db.SourceBooks.Add(created);
        return created;
    }

    private void DetachPending()
    {
        if (db is DbContext context) context.ChangeTracker.Clear();
    }

    private static TEnum? ParseEnum<TEnum>(string? raw, string field, Dictionary<string, string[]> errors) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return FieldValueConverter.ConvertTo<TEnum>(JsonSerializer.SerializeToElement(raw), field);
        }
        catch (RequestValidationException)
        {
            errors[field] = [$"Valor '{raw}' inválido. Opções: {string.Join(", ", Enum.GetNames<TEnum>())}."];
            return null;
        }
    }

    private static bool IsAllowedImageUrl(string url) =>
        url.StartsWith('/') ||
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Neutralise spreadsheet formula injection and quote fields that need it.
        if (value[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
