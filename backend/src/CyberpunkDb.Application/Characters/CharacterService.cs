using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters;

/// <summary>
/// Persistence and commands of player characters. Every change goes through here: the sheet is loaded, the
/// client's version checked, the operation applied by the rules services, and the sheet saved together with its
/// history entries. Each character is a separate row, so changing one never touches another.
/// </summary>
public sealed class CharacterService(
    IAppDbContext db,
    CharacterRuleBook rules,
    CharacterCalculator calculator,
    CharacterValidator validator,
    CharacterCreationService creation,
    InventoryOperations inventory,
    CyberwareOperations cyberware,
    ProgressionOperations progression,
    EconomyOperations economy,
    CombatOperations combat,
    WearableOperations wearables,
    NpcCatalogProvider catalogs,
    ICharacterObserver observer)
{
    internal static readonly JsonSerializerOptions SheetJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public const int PortraitMaxBytes = 2 * 1024 * 1024;
    private static readonly string[] PortraitTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    // ------------------------------------------------------------ queries

    public async Task<CharacterRulesDto> GetRulesAsync(CancellationToken ct) =>
        CharacterRulesMapper.Map(rules, await catalogs.GetAsync(ct));

    /// <summary>
    /// One page of priced catalog items matching a name (accents and case ignored), optionally from one category.
    /// Descriptions are read only for the items on the page.
    /// </summary>
    public async Task<StorePageDto> StoreAsync(string? query, ItemCategory? category, int page, int pageSize, CancellationToken ct,
        StoreSort sort = StoreSort.Category, bool descending = false)
    {
        var catalog = await catalogs.GetAsync(ct);
        var q = TextNormalizer.Normalize(query ?? "");
        var filtered = catalog.Items
            .Where(i => i.Price is not null && (category is null || i.Category == category))
            .Where(i => q.Length == 0 || TextNormalizer.Normalize(i.Name).Contains(q) || i.Slug.Contains(q, StringComparison.OrdinalIgnoreCase));
        // The whole catalog is sorted before paging, so every page follows the chosen column.
        var matches = (sort switch
        {
            StoreSort.Name => descending ? filtered.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase) : filtered.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            StoreSort.Price => (descending ? filtered.OrderByDescending(i => i.Price) : filtered.OrderBy(i => i.Price)).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            _ => (descending ? filtered.OrderByDescending(i => i.Category) : filtered.OrderBy(i => i.Category)).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();

        pageSize = Math.Clamp(pageSize, 1, 1000);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matches.Count / (double)pageSize)));
        var slice = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ids = slice.Select(i => i.Id).ToList();
        var descriptions = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Description }).ToDictionaryAsync(x => x.Id, x => x.Description, ct);

        var items = slice.Select(i => new StoreItemDto(i.Id, i.Slug, i.Name, i.Category, i.Price, i.SourceBook, i.Page,
                CharacterValidator.IsFashion(i), i.Cyber is not null && i.Ammo is null,
                i.Ammo?.Calibers ?? i.Weapon?.Calibers ?? [], i.Ammo?.PerPack, StoreSummary(i),
                i.Cyber?.Type, i.Cyber?.Location, i.Cyber?.HumanityLoss, i.Cyber?.HumanityLossDice, i.Cyber?.Foundational ?? false,
                i.Cyber?.Requirements, descriptions.GetValueOrDefault(i.Id), i.Cyber?.Effect))
            .ToList();
        return new StorePageDto(items, matches.Count, page, pageSize);
    }

    private static string? StoreSummary(CatalogItem i) => i switch
    {
        { Weapon: { } w } => L.T($"{w.Damage} · ROF {w.Rof}{(w.Magazine is { } m ? $" · pente {m}" : "")}", $"{w.Damage} · ROF {w.Rof}{(w.Magazine is { } m2 ? $" · magazine {m2}" : "")}"),
        { Armor: { Sp: { } sp } a } => $"SP {sp}" + (a.RefPenalty > 0 ? $" · -{a.RefPenalty} REF/DEX/MOVE" : ""),
        { Cyber: { } c } => $"{c.Type} · HL {c.HumanityLoss}{(c.HumanityLossDice is { } d ? $" ({d})" : "")}",
        { Ammo: { PerPack: { } p } } => L.T($"pacote com {p}", $"pack of {p}"),
        { Attachment: { } a } => L.T($"{a.Type} · {(a.SlotsRequired == 0 ? "sem slot" : $"{a.SlotsRequired} slot(s)")}{(a.ExoticAllowed ? " · aceita exóticas" : "")}", $"{a.Type} · {(a.SlotsRequired == 0 ? "no slot" : $"{a.SlotsRequired} slot(s)")}{(a.ExoticAllowed ? " · takes exotics" : "")}"),
        _ => null,
    };

    public async Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var rows = await db.Characters.AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { c.Id, c.SheetJson, c.CreationMethod, c.CreatedAt, c.UpdatedAt, HasPortrait = c.Portrait != null })
            .ToListAsync(ct);
        return rows.Select(r =>
        {
            var sheet = Deserialize(r.SheetJson);
            var report = calculator.Calculate(sheet, catalog);
            var validation = validator.Validate(sheet, catalog);
            return new CharacterSummaryDto(r.Id, sheet.Identity.Name, sheet.Identity.Handle, sheet.PrimaryRole?.Role, sheet.PrimaryRole?.Rank,
                r.CreationMethod, validation.Status, validation.ErrorCount, validation.WarningCount,
                report.Health.MaxHp, report.Health.CurrentHp, report.Humanity.Current, report.Humanity.Max, sheet.Wallet.Balance,
                r.HasPortrait, r.CreatedAt, r.UpdatedAt, sheet.Identity.PortraitFocus);
        }).ToList();
    }

    public async Task<CharacterDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        var hasPortrait = await db.CharacterPortraits.AnyAsync(p => p.CharacterId == id, ct);
        return Detail(entity, Deserialize(entity.SheetJson), await catalogs.GetAsync(ct), hasPortrait, []);
    }

    /// <summary>The stored sheet and its version, as the combat table keeps it to undo an attack.</summary>
    /// <summary>The portrait framing stored in a sheet's JSON (the table's tokens use it without loading the whole report).</summary>
    internal static PortraitFocus? PortraitFocusOf(string sheetJson)
    {
        try
        {
            return Deserialize(sheetJson).Identity.PortraitFocus?.Clamp();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal async Task<(int Version, string SheetJson)> SnapshotAsync(int id, CancellationToken ct)
    {
        var row = await db.Characters.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.Version, c.SheetJson }).FirstOrDefaultAsync(ct)
                  ?? throw NotFound(id);
        return (row.Version, row.SheetJson);
    }

    /// <summary>
    /// Puts back a sheet taken before a combat action (the undo of the last attack), as long as nothing changed the sheet
    /// since: it must still be at <paramref name="version"/>.
    /// </summary>
    internal async Task RestoreAsync(int id, int version, string sheetJson, string description, CancellationToken ct)
    {
        var entity = await db.Characters.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        if (entity.Version != version)
            throw new ConflictException(L.T($"A ficha de {entity.Name} mudou depois do ataque; desfaça à mão.", $"{entity.Name}'s sheet changed after the attack; undo it by hand."));
        var now = DateTime.UtcNow;
        Apply(entity, Deserialize(sheetJson), await catalogs.GetAsync(ct), now);
        AddEvents(entity, [new PendingEvent(CharacterEventKind.Combat, "undo-attack", description, null, null, null, false)], now);
        await db.SaveChangesAsync(ct);    await observer.ChangedAsync(id, ct);
    }

    /// <summary>The history, newest first. IP spends on skills and Role Ranks say whether they can still be undone.</summary>
    public async Task<IReadOnlyList<CharacterEventDto>> EventsAsync(int id, CharacterEventKind? kind, CancellationToken ct)
    {
        var sheetJson = await db.Characters.AsNoTracking().Where(c => c.Id == id).Select(c => c.SheetJson).FirstOrDefaultAsync(ct) ?? throw NotFound(id);
        var sheet = Deserialize(sheetJson);
        var undone = await UndoneAsync(id, ct);
        var query = db.CharacterEvents.AsNoTracking().Where(e => e.CharacterId == id);
        if (kind is not null) query = query.Where(e => e.Kind == kind);
        var rows = await query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).Take(500).ToListAsync(ct);
        return rows.Select(e =>
        {
            var step = progression.StepOf(e.Action, e.PayloadJson, e.Description);
            var isUndone = undone.Contains(e.Id);
            return new CharacterEventDto(e.Id, e.Kind, e.Action, e.Amount, e.Before, e.After, e.Description, e.IsManual, e.CreatedAt,
                CanUndo: step is not null && !isUndone && progression.CanUndo(sheet, step), Undone: isUndone);
        }).ToList();
    }

    /// <summary>Undoes one IP spend of the history (a skill level or a Role Rank): the level goes back and the IP return.</summary>
    public async Task<CharacterDetailDto> UndoProgressionAsync(int id, UndoProgressionRequest r, CancellationToken ct)
    {
        var e = await db.CharacterEvents.AsNoTracking().FirstOrDefaultAsync(x => x.CharacterId == id && x.Id == r.EventId, ct)
                ?? throw new NotFoundException(L.T("Registro do histórico não encontrado.", "History entry not found."));
        if ((await UndoneAsync(id, ct)).Contains(e.Id))
            throw RequestValidationException.For("eventId", L.T("Esse passo já foi desfeito.", "That step was already undone."));
        var step = progression.StepOf(e.Action, e.PayloadJson, e.Description)
                   ?? throw RequestValidationException.For("eventId", L.T("Este registro não pode ser desfeito.", "This entry can't be undone."));
        return await MutateAsync(id, r.Version, c => progression.Undo(c.Sheet, step, e.Id, e.Description, c), ct);
    }

    /// <summary>Ids of the IP spends already undone (each refund points at the spend it reverses).</summary>
    private async Task<HashSet<long>> UndoneAsync(int id, CancellationToken ct) =>
        (await db.CharacterEvents.AsNoTracking().Where(e => e.CharacterId == id && e.Action == ProgressionOperations.RefundAction && e.PayloadJson != null)
            .Select(e => e.PayloadJson!).ToListAsync(ct))
        .Select(ProgressionOperations.UndoneId).OfType<long>().ToHashSet();

    // ------------------------------------------------------------ create, duplicate, delete

    public async Task<CharacterDetailDto> CreateAsync(CreateCharacterRequest request, CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var change = new CharacterChange(new CharacterSheet(), catalog, now);
        var sheet = creation.NewDraft(request, change).Normalize();
        change.Log(CharacterEventKind.Edit, "create", L.T($"Personagem criado ({request.Method}, {request.Role}).", $"Character created ({request.Method}, {request.Role})."));

        var entity = new Character { CreatedAt = now };
        Apply(entity, sheet, catalog, now);
        AddEvents(entity, change.Events, now);
        db.Characters.Add(entity);
        await db.SaveChangesAsync(ct);
        return Detail(entity, sheet, catalog, false, change.Notices);
    }

    public async Task<CharacterDetailDto> DuplicateAsync(int id, CancellationToken ct)
    {
        var source = await db.Characters.AsNoTracking().Include(c => c.Portrait).FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var sheet = Deserialize(source.SheetJson);
        sheet = sheet with { Identity = sheet.Identity with { Name = L.T($"{sheet.Identity.Name} (cópia)", $"{sheet.Identity.Name} (copy)").Trim() } };

        var copy = new Character { CreatedAt = now };
        Apply(copy, sheet, catalog, now);
        if (source.Portrait is { } p)
            copy.Portrait = new CharacterPortrait { ContentType = p.ContentType, Data = p.Data, UpdatedAt = now };
        AddEvents(copy, [new PendingEvent(CharacterEventKind.Edit, "duplicate", L.T($"Cópia de \"{Deserialize(source.SheetJson).Identity.Name}\" (#{source.Id}); o histórico não foi copiado.", $"Copy of \"{Deserialize(source.SheetJson).Identity.Name}\" (#{source.Id}); the history wasn't copied."), null, null, null, false)], now);
        db.Characters.Add(copy);
        await db.SaveChangesAsync(ct);
        return Detail(copy, sheet, catalog, copy.Portrait is not null, []);
    }

    // ------------------------------------------------------------ export and import

    /// <summary>Largest import accepted: the sheet and history plus a 2 MB portrait in base64.</summary>
    public const int ImportMaxBytes = 12 * 1024 * 1024;

    /// <summary>One character as a file: sheet, catalog references by slug, history and portrait.</summary>
    public async Task<(byte[] Content, string FileName)> ExportAsync(int id, CancellationToken ct)
    {
        var entity = await db.Characters.AsNoTracking().Include(c => c.Portrait).FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        var sheet = Deserialize(entity.SheetJson);
        var history = await db.CharacterEvents.AsNoTracking().Where(e => e.CharacterId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ExportedEvent(e.Kind, e.Action, e.Amount, e.Before, e.After, e.Description, e.IsManual, e.CreatedAt))
            .ToListAsync(ct);
        var file = new CharacterExportFile
        {
            Format = CharacterExportFile.FormatName,
            FormatVersion = CharacterExportFile.CurrentVersion,
            ExportedAt = DateTime.UtcNow,
            Sheet = sheet,
            Items = CharacterTransfer.ReferencedItems(sheet, await catalogs.GetAsync(ct)),
            History = history,
            Portrait = entity.Portrait is { } p ? new ExportedPortrait(p.ContentType, Convert.ToBase64String(p.Data)) : null,
        };
        return (JsonSerializer.SerializeToUtf8Bytes(file, ExportJson), CharacterTransfer.FileName(sheet.Identity));
    }

    private static readonly JsonSerializerOptions ExportJson = new(SheetJson) { WriteIndented = true };

    /// <summary>
    /// Creates a new character from an exported file; an existing one is never overwritten. Catalog items are found
    /// again by slug, and the ones this catalog lacks are left out and reported. The sheet goes through the same
    /// validation as any other, so a hand-edited file comes in with its problems listed rather than refused.
    /// </summary>
    public async Task<CharacterDetailDto> ImportAsync(CharacterExportFile file, CancellationToken ct)
    {
        if (file.Format != CharacterExportFile.FormatName)
            throw RequestValidationException.For("format", L.T("Este arquivo não é um personagem exportado por este aplicativo.", "This file isn't a character exported by this app."));
        if (file.FormatVersion is < 1 or > CharacterExportFile.CurrentVersion)
            throw RequestValidationException.For("formatVersion",
                L.T($"O arquivo usa a versão {file.FormatVersion} do formato; este aplicativo lê até a {CharacterExportFile.CurrentVersion}. Atualize o aplicativo.", $"The file uses format version {file.FormatVersion}; this app reads up to {CharacterExportFile.CurrentVersion}. Update the app."));
        if (file.Sheet is null) throw RequestValidationException.For("sheet", L.T("O arquivo não traz a ficha do personagem.", "The file doesn't contain the character sheet."));

        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var (sheet, missing) = CharacterTransfer.Rebind(file.Sheet.Normalize(), file.Items ?? [], catalog);
        if (sheet.Roles.Count == 0) throw RequestValidationException.For("sheet", L.T("A ficha não tem Role.", "The sheet has no Role."));
        if (sheet.Roles.FirstOrDefault(r => rules.FindRole(r.Role) is null) is { } unknown)
            throw RequestValidationException.For("sheet", L.T($"Role desconhecida na ficha: {unknown.Role}.", $"Unknown Role on the sheet: {unknown.Role}."));

        // Same name as a character already here: "(importado)", then "(importado 2)"... so they stay apart in lists.
        var name = string.IsNullOrWhiteSpace(sheet.Identity.Name) ? L.T("Sem nome", "Unnamed") : sheet.Identity.Name;
        var taken = (await db.Characters.Where(c => c.Name.StartsWith(name)).Select(c => c.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (taken.Contains(name))
        {
            var renamed = $"{name} (importado)";
            for (var n = 2; taken.Contains(renamed); n++) renamed = $"{name} (importado {n})";
            sheet = sheet with { Identity = sheet.Identity with { Name = renamed } };
        }

        var notices = missing.Select(m => L.T($"Fora do catálogo deste banco, não importado: {m}.", $"Not in this database's catalog, not imported: {m}.")).ToList();
        var entity = new Character { CreatedAt = now };
        Apply(entity, sheet, catalog, now);

        if (file.Portrait is { } portrait)
        {
            byte[]? data = null;
            try { data = Convert.FromBase64String(portrait.Data ?? ""); } catch (FormatException) { }
            if (data is { Length: > 0 and <= PortraitMaxBytes } && PortraitTypes.Contains(portrait.ContentType, StringComparer.OrdinalIgnoreCase))
                entity.Portrait = new CharacterPortrait { ContentType = portrait.ContentType, Data = data, UpdatedAt = now };
            else notices.Add(L.T("O retrato do arquivo é inválido e ficou de fora.", "The file's portrait is invalid and was left out."));
        }

        foreach (var e in (file.History ?? []).TakeLast(CharacterTransfer.MaxHistory))
            entity.Events.Add(new CharacterEvent
            {
                Kind = e.Kind, Action = Truncate(e.Action ?? "", 60)!, Description = Truncate(e.Description ?? "", 500)!, Amount = e.Amount,
                Before = Truncate(e.Before, 200), After = Truncate(e.After, 200), IsManual = e.IsManual,
                CreatedAt = e.CreatedAt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc) : e.CreatedAt.ToUniversalTime(),
            });
        AddEvents(entity, [new PendingEvent(CharacterEventKind.Edit, "import",
            L.T($"Importado de arquivo exportado em {file.ExportedAt:dd/MM/yyyy}.", $"Imported from a file exported on {file.ExportedAt:yyyy-MM-dd}.") +
            (missing.Count > 0 ? L.T($" Fora do catálogo, não importado: {string.Join(", ", missing)}.", $" Not in the catalog, not imported: {string.Join(", ", missing)}.") : ""), null, null, null, false)], now);

        db.Characters.Add(entity);
        await db.SaveChangesAsync(ct);
        return Detail(entity, sheet, catalog, entity.Portrait is not null, notices);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Characters.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        db.Characters.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ editing the sheet

    /// <summary>
    /// Saves the editable sections. Inventory, cyberware, money, Humanity, combat, injuries and progress are kept from
    /// the stored sheet: they only change through commands. After creation, STATs, skills and Roles are kept too,
    /// unless a manual reason is given (a GM-approved adjustment, logged with what changed).
    /// </summary>
    public Task<CharacterDetailDto> SaveSheetAsync(int id, SaveSheetRequest request, CancellationToken ct) =>
        MutateAsync(id, request.Version, change =>
        {
            var current = change.Sheet;
            var incoming = request.Sheet.Normalize();
            var manual = !string.IsNullOrWhiteSpace(request.ManualReason);
            var finalized = current.Creation.Finalized;

            var merged = current with
            {
                Identity = incoming.Identity,
                Modifiers = incoming.Modifiers,
                Lifepath = incoming.Lifepath,
                RoleLifepath = incoming.RoleLifepath,
                Style = incoming.Style,
                Housing = incoming.Housing,
                Notes = incoming.Notes,
            };
            if (!finalized)
            {
                merged = merged with
                {
                    Stats = incoming.Stats,
                    Skills = incoming.Skills,
                    Creation = current.Creation with
                    {
                        StartingChoices = incoming.Creation.StartingChoices,
                        Overrides = incoming.Creation.Overrides,
                    },
                };
                merged = creation.RekeyIdentitySkills(current, merged);
            }
            else
            {
                var changes = Differences(current, incoming);
                if (changes.Count > 0 && !manual)
                    throw CharacterChange.Refuse("sheet",
                        L.T($"Depois da criação, {string.Join(", ", changes)} só mudam por ação de regra (IP, compra, instalação). ", $"After creation, {string.Join(", ", changes)} only change through a rule action (IP, purchase, install). ") +
                        L.T("Para um ajuste do Mestre, informe o motivo.", "For a GM adjustment, give the reason."));
                if (changes.Count > 0)
                {
                    merged = merged with { Stats = incoming.Stats, Skills = incoming.Skills, Roles = incoming.Roles };
                    change.Log(CharacterEventKind.Edit, "manual-adjustment",
                        L.T($"Ajuste manual ({string.Join(", ", changes)}): {request.ManualReason!.Trim()}", $"Manual adjustment ({string.Join(", ", changes)}): {request.ManualReason!.Trim()}"), manual: true);
                }
            }
            return merged;
        }, ct);

    private static List<string> Differences(CharacterSheet a, CharacterSheet b)
    {
        var list = new List<string>();
        if (!SameMap(a.Stats, b.Stats)) list.Add("STATs");
        if (!SameMap(a.Skills, b.Skills)) list.Add(L.T("perícias", "skills"));
        if (a.Roles.Count != b.Roles.Count || a.Roles.Zip(b.Roles).Any(p => p.First.Role != p.Second.Role || p.First.Rank != p.Second.Rank))
            list.Add("Roles");
        return list;

        static bool SameMap(Dictionary<string, int> x, Dictionary<string, int> y) =>
            x.Count == y.Count && x.All(kv => y.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    // ------------------------------------------------------------ commands

    public Task<CharacterDetailDto> ResetCreationAsync(int id, ResetCreationRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => creation.Reset(c.Sheet, r.Method, r.Role, c), ct);

    public Task<CharacterDetailDto> TemplateRowAsync(int id, TemplateRowRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => creation.ApplyTemplateRow(c.Sheet, r.Row, c), ct);

    public Task<CharacterDetailDto> StatRollsAsync(int id, StatRollsRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => creation.ApplyStatRolls(c.Sheet, r.Rolls, c), ct);

    public Task<CharacterDetailDto> SkillChoiceAsync(int id, SkillChoiceRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => creation.ChooseSpecialization(c.Sheet, r.SkillId, r.Specialization, c), ct);

    public Task<CharacterDetailDto> FinalizeAsync(int id, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => creation.Finalize(c.Sheet, c), ct);

    public Task<CharacterDetailDto> PurchaseAsync(int id, PurchaseRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Purchase(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> UndoPurchaseAsync(int id, InstanceRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.UndoPurchase(c.Sheet, r.InstanceId, c), ct);

    public Task<CharacterDetailDto> EquipAsync(int id, EquipRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Equip(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> FireAsync(int id, FireRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Fire(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> ReloadAsync(int id, ReloadRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Reload(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> AdjustBalanceAsync(int id, AdjustBalanceRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => InventoryOperations.AdjustBalance(c.Sheet, r, c), ct);

    public async Task<InstallPreviewDto> PreviewInstallAsync(int id, InstallCyberwareRequest r, CancellationToken ct)
    {
        var entity = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        return cyberware.Preview(Deserialize(entity.SheetJson), r, await catalogs.GetAsync(ct));
    }

    public Task<CharacterDetailDto> InstallCyberwareAsync(int id, InstallCyberwareRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => cyberware.Install(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> RemoveCyberwareAsync(int id, RemoveCyberwareRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => cyberware.Remove(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> ReplaceCyberwareAsync(int id, ReplaceCyberwareRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => cyberware.Replace(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> TherapyAsync(int id, TherapyRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => cyberware.Therapy(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> AttachAsync(int id, AttachRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Attach(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> DetachAsync(int id, AttachRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => inventory.Detach(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> WearableInstallAsync(int id, WearableInstallRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => wearables.Install(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> WearableRemoveAsync(int id, WearableOptionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => wearables.Remove(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> WearableSideAsync(int id, WearableSideRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => wearables.SetSide(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> AwardIpAsync(int id, AwardIpRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => progression.AwardIp(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> ImproveSkillAsync(int id, ImproveSkillRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => progression.ImproveSkill(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> ImproveRoleAsync(int id, ImproveRoleRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => progression.ImproveRole(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> AddRoleAsync(int id, AddRoleRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => progression.AddRole(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> PayMonthsAsync(int id, PayMonthsRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => economy.PayMonths(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> RemoveItemAsync(int id, RemoveItemRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => EconomyOperations.RemoveItem(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> LuckAsync(int id, LuckRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => InventoryOperations.SetLuckSpent(c.Sheet, r.Spent, c), ct);

    /// <summary>Takes <paramref name="points"/> from this session's LUCK Pool (CP:R p.130); refuses what the pool doesn't hold.</summary>
    public async Task<CharacterDetailDto> SpendLuckAsync(int id, int points, CancellationToken ct)
    {
        var detail = await GetAsync(id, ct);
        if (points > detail.Report.LuckAvailable)
            throw RequestValidationException.For("luck", L.T($"Sorte insuficiente: restam {detail.Report.LuckAvailable} pontos nesta sessão.", $"Not enough LUCK: {detail.Report.LuckAvailable} points left this session."));
        return await LuckAsync(id, new LuckRequest(detail.Version, detail.Sheet.Combat.LuckSpent + points), ct);
    }

    public Task<CharacterDetailDto> DamageAsync(int id, DamageRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.Damage(RequireFinalized(c.Sheet), r, c), ct);

    public Task<CharacterDetailDto> HealAsync(int id, HealRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.Heal(RequireFinalized(c.Sheet), r, c), ct);

    public Task<CharacterDetailDto> StabilizeAsync(int id, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.Stabilize(RequireFinalized(c.Sheet), c), ct);

    public Task<CharacterDetailDto> DeathSaveAsync(int id, DeathSaveRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.DeathSave(RequireFinalized(c.Sheet), r, c), ct);

    public Task<CharacterDetailDto> AblateAsync(int id, AblateRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.AblateManual(c.Sheet, r, c), ct);

    public Task<CharacterDetailDto> RepairArmorAsync(int id, RepairArmorRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.RepairArmor(c.Sheet, r, c), ct);

    /// <summary>
    /// SP of the armor on one location up or down by <paramref name="delta"/> (the virtual table's +/−): down ablates,
    /// up repairs, never past the armor's full SP.
    /// </summary>
    public async Task<CharacterDetailDto> AdjustArmorAsync(int id, ArmorSlot location, int delta, string description, CancellationToken ct)
    {
        var detail = await GetAsync(id, ct);
        if (delta == 0) return detail;
        if (delta < 0) return await AblateAsync(id, new AblateRequest(detail.Version, location, -delta, description), ct);
        var armor = location == ArmorSlot.Head ? detail.Report.Armor.Head : detail.Report.Armor.Body;
        if (armor.InstanceId is null || armor.MaxSp is not { } max)
            throw RequestValidationException.For("location", L.T("Não há armadura reparável nesse local.", "There is no repairable armor on that location."));
        return await RepairArmorAsync(id, new RepairArmorRequest(detail.Version, armor.InstanceId, Math.Min(max, armor.Sp + delta)), ct);
    }

    public Task<CharacterDetailDto> AddInjuryAsync(int id, AddInjuryRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.AddInjury(RequireFinalized(c.Sheet), r, c), ct);

    public Task<CharacterDetailDto> UpdateInjuryAsync(int id, string injuryId, UpdateInjuryRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => combat.UpdateInjury(c.Sheet, injuryId, r, c), ct);

    public Task<CharacterDetailDto> RemoveInjuryAsync(int id, string injuryId, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => CombatOperations.RemoveInjury(c.Sheet, injuryId, c), ct);

    public Task<CharacterDetailDto> ClearConditionsAsync(int id, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, c => CombatOperations.ClearConditions(c.Sheet, c), ct);

    private static CharacterSheet RequireFinalized(CharacterSheet sheet) =>
        sheet.Creation.Finalized ? sheet : throw CharacterChange.Refuse("creation", L.T("Conclua a criação antes de usar o painel de combate.", "Finish creation before using the combat panel."));

    // ------------------------------------------------------------ portrait

    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> GetPortraitAsync(int id, CancellationToken ct)
    {
        var p = await db.CharacterPortraits.AsNoTracking().FirstOrDefaultAsync(x => x.CharacterId == id, ct)
                ?? throw new NotFoundException(L.T("Este personagem não tem retrato.", "This character has no portrait."));
        return (p.Data, p.ContentType, p.UpdatedAt);
    }

    public async Task SetPortraitAsync(int id, string contentType, Stream content, long length, CancellationToken ct)
    {
        RequirePortraitFile(contentType, length);
        var entity = await db.Characters.Include(c => c.Portrait).FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var now = DateTime.UtcNow;
        if (entity.Portrait is null) entity.Portrait = new CharacterPortrait { ContentType = contentType, Data = buffer.ToArray(), UpdatedAt = now };
        else
        {
            entity.Portrait.ContentType = contentType;
            entity.Portrait.Data = buffer.ToArray();
            entity.Portrait.UpdatedAt = now;
        }
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await observer.ChangedAsync(id, ct);
    }

    /// <summary>The image rules shared by every stored portrait (characters, saved NPCs, compendium NPCs).</summary>
    internal static void RequirePortraitFile(string contentType, long length)
    {
        if (!PortraitTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw RequestValidationException.For("file", L.T("Use uma imagem PNG, JPEG, WebP ou GIF.", "Use a PNG, JPEG, WebP or GIF image."));
        if (length <= 0 || length > PortraitMaxBytes)
            throw RequestValidationException.For("file", L.T("A imagem deve ter até 2 MB.", "The image must be 2 MB or less."));
    }

    public async Task DeletePortraitAsync(int id, CancellationToken ct)
    {
        var p = await db.CharacterPortraits.FirstOrDefaultAsync(x => x.CharacterId == id, ct);
        if (p is null) return;
        db.CharacterPortraits.Remove(p);
        await db.SaveChangesAsync(ct);
        await observer.ChangedAsync(id, ct);
    }

    // ------------------------------------------------------------ plumbing

    private async Task<CharacterDetailDto> MutateAsync(int id, int version, Func<CharacterChange, CharacterSheet> operation, CancellationToken ct)
    {
        var entity = await db.Characters.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound(id);
        if (entity.Version != version)
            throw new ConflictException(L.T("Esta ficha foi alterada em outra janela. Recarregue para ver a versão atual.", "This sheet was changed in another window. Reload to see the current version."));

        var catalog = await catalogs.GetAsync(ct);
        var now = DateTime.UtcNow;
        var change = new CharacterChange(Deserialize(entity.SheetJson), catalog, now);
        var sheet = operation(change).Normalize();

        Apply(entity, sheet, catalog, now);
        AddEvents(entity, change.Events, now);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(L.T("Esta ficha foi alterada ao mesmo tempo em outra janela. Recarregue e tente de novo.", "This sheet was changed at the same time in another window. Reload and try again."));
        }
        var hasPortrait = await db.CharacterPortraits.AnyAsync(p => p.CharacterId == id, ct);
        var detail = Detail(entity, sheet, catalog, hasPortrait, change.Notices);
        await observer.ChangedAsync(id, ct);
        return detail;
    }

    private void Apply(Character entity, CharacterSheet sheet, NpcCatalog catalog, DateTime now)
    {
        var status = validator.Validate(sheet, catalog).Status;
        entity.Name = Truncate(string.IsNullOrWhiteSpace(sheet.Identity.Name) ? L.T("Sem nome", "Unnamed") : sheet.Identity.Name, 80)!;
        entity.Handle = Truncate(sheet.Identity.Handle, 80);
        entity.Role = sheet.PrimaryRole?.Role;
        entity.CreationMethod = sheet.Creation.Method;
        entity.Status = status;
        entity.SheetJson = JsonSerializer.Serialize(sheet, SheetJson);
        entity.UpdatedAt = now;
        entity.Version++;
    }

    private static void AddEvents(Character entity, IEnumerable<PendingEvent> events, DateTime now)
    {
        foreach (var e in events)
            entity.Events.Add(new CharacterEvent
            {
                Kind = e.Kind, Action = e.Action, Description = Truncate(e.Description, 500)!, Amount = e.Amount,
                Before = Truncate(e.Before, 200), After = Truncate(e.After, 200), IsManual = e.IsManual, PayloadJson = e.Payload, CreatedAt = now,
            });
    }

    private CharacterDetailDto Detail(Character entity, CharacterSheet sheet, NpcCatalog catalog, bool hasPortrait, IReadOnlyList<string> notices) =>
        new(entity.Id, entity.Version, entity.Status, hasPortrait, entity.CreatedAt, entity.UpdatedAt,
            sheet, calculator.Calculate(sheet, catalog), validator.Validate(sheet, catalog), notices);

    internal static CharacterSheet Deserialize(string json) =>
        (JsonSerializer.Deserialize<CharacterSheet>(json, SheetJson) ?? new CharacterSheet()).Normalize();

    private static NotFoundException NotFound(int id) => new(L.T($"Personagem {id} não encontrado.", $"Character {id} not found."));

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
