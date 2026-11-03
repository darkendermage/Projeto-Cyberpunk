using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// Scenes of the virtual table: prepared ahead, reused, one active at a time. The GM edits everything; a player moves
/// only their own tokens and their own area templates on the active scene. Every write goes out to the open screens.
/// Settings edits (map, grid, linked fight) carry the scene version; token and template changes are last-write-wins,
/// serialized per scene.
/// </summary>
public sealed class SceneService(IAppDbContext db, SceneLocks locks, ITableNotifier notifier)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public const int MaxTokens = 300;
    public const int MaxTemplates = 100;
    private const double MinCell = -100, MaxCell = 2000;

    // ------------------------------------------------------------------ library

    public async Task<IReadOnlyList<SceneSummaryDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Scenes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        return rows.Select(s =>
        {
            var state = Deserialize(s.StateJson);
            return new SceneSummaryDto(s.Id, s.Name, s.Active, s.SortOrder, s.EncounterId,
                state.Background.MediaId is { } m ? MediaService.UrlOf(m) : null, state.Tokens.Count, s.UpdatedAt);
        }).ToList();
    }

    public async Task<SceneView> GetAsync(int id, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFound();
        var state = Deserialize(scene.StateJson);
        return SceneViews.ForGm(scene, state, await PortraitsAsync(state, ct));
    }

    /// <summary>A new, empty scene at the end of the list. The very first scene starts active.</summary>
    public async Task<SceneView> CreateAsync(CreateSceneRequest r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var scene = new Scene
        {
            Name = RequireName(r.Name),
            SortOrder = (await db.Scenes.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0) + 1,
            Active = !await db.Scenes.AnyAsync(ct),
            StateJson = Serialize(new SceneState()),
            CreatedAt = now, UpdatedAt = now,
        };
        db.Scenes.Add(scene);
        await db.SaveChangesAsync(ct);
        return await PublishAsync(scene, new SceneState(), ct);
    }

    public Task<SceneView> UpdateAsync(int id, UpdateSceneRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (scene, state) =>
        {
            scene.Name = RequireName(r.Name);
            if (r.EncounterId is { } encounterId && encounterId != scene.EncounterId && !await db.Encounters.AnyAsync(e => e.Id == encounterId, ct))
                throw RequestValidationException.For("encounterId", L.T("Combate não encontrado.", "Fight not found."));
            scene.EncounterId = r.EncounterId;
            if (r.BackgroundMediaId is { } media) await RequireMediaAsync(media, "backgroundMediaId", ct);
            if (r.BackgroundWidth is < 0 or > 20000 || r.BackgroundHeight is < 0 or > 20000)
                throw RequestValidationException.For("backgroundWidth", L.T("Tamanho de mapa inválido.", "Invalid map size."));
            return state with
            {
                Grid = Grid(r.Grid),
                Background = new SceneBackground { MediaId = r.BackgroundMediaId, Width = r.BackgroundWidth, Height = r.BackgroundHeight },
            };
        }, bumpVersion: true, ct);

    /// <summary>A copy to prepare a variant (the same bar before and after the shootout); it starts inactive.</summary>
    public async Task<SceneView> DuplicateAsync(int id, CancellationToken ct)
    {
        var source = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFound();
        var now = DateTime.UtcNow;
        var name = L.T($"{source.Name} (cópia)", $"{source.Name} (copy)");
        var copy = new Scene
        {
            Name = name.Length <= 80 ? name : name[..80], EncounterId = source.EncounterId, StateJson = source.StateJson,
            SortOrder = (await db.Scenes.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0) + 1, CreatedAt = now, UpdatedAt = now,
        };
        db.Scenes.Add(copy);
        await db.SaveChangesAsync(ct);
        return await PublishAsync(copy, Deserialize(copy.StateJson), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(id, ct);
        var scene = await db.Scenes.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFound();
        db.Scenes.Remove(scene);
        await db.SaveChangesAsync(ct);
        await notifier.SceneRemovedAsync(id, scene.Active ? await NoSceneAsync(ct) : null, ct);
    }

    /// <summary>Shows this scene to the players: every screen switches at once.</summary>
    public async Task<SceneView> ActivateAsync(int id, CancellationToken ct)
    {
        var all = await db.Scenes.ToListAsync(ct);
        var scene = all.FirstOrDefault(s => s.Id == id) ?? throw NotFound();
        foreach (var s in all) s.Active = s.Id == id;
        await db.SaveChangesAsync(ct);
        return await PublishAsync(scene, Deserialize(scene.StateJson), ct);
    }

    public async Task<IReadOnlyList<SceneSummaryDto>> ReorderAsync(ReorderScenesRequest r, CancellationToken ct)
    {
        var all = await db.Scenes.ToListAsync(ct);
        var order = r.Ids.Distinct().ToList();
        foreach (var s in all)
        {
            var index = order.IndexOf(s.Id);
            s.SortOrder = index < 0 ? order.Count + s.SortOrder : index + 1;
        }
        await db.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    // ------------------------------------------------------------------ tokens and templates (GM)

    public Task<SceneView> AddTokenAsync(int id, TokenInput input, CancellationToken ct) =>
        MutateAsync(id, null, async (_, state) =>
        {
            if (state.Tokens.Count >= MaxTokens)
                throw RequestValidationException.For("tokens", L.T($"Uma cena comporta até {MaxTokens} tokens.", $"A scene holds up to {MaxTokens} tokens."));
            var token = await TokenAsync(NextId("T", state.Tokens.Select(t => t.Id)), input, ct);
            return state with { Tokens = [.. state.Tokens, token] };
        }, bumpVersion: false, ct);

    public Task<SceneView> UpdateTokenAsync(int id, string tokenId, TokenInput input, CancellationToken ct) =>
        MutateAsync(id, null, async (_, state) =>
        {
            RequireToken(state, tokenId);
            var token = await TokenAsync(tokenId, input, ct);
            return state with { Tokens = state.Tokens.Select(t => t.Id == tokenId ? token : t).ToList() };
        }, bumpVersion: false, ct);

    /// <summary>The GM moves any token, locked or not.</summary>
    public Task<SceneView> MoveTokenAsync(int id, string tokenId, TokenMoveRequest move, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => Task.FromResult(Move(state, RequireToken(state, tokenId), move)), bumpVersion: false, ct);

    public Task<SceneView> RemoveTokenAsync(int id, string tokenId, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            RequireToken(state, tokenId);
            return Task.FromResult(state with { Tokens = state.Tokens.Where(t => t.Id != tokenId).ToList() });
        }, bumpVersion: false, ct);

    /// <summary>
    /// One token per combatant of the linked fight that has none yet: allies on the first row, enemies two rows below.
    /// The GM then drags them to their starting positions, and the scene keeps them for the session. On a live scene the
    /// enemies' tokens start hidden.
    /// </summary>
    public Task<SceneView> AddEncounterTokensAsync(int id, CancellationToken ct) =>
        MutateAsync(id, null, async (scene, state) =>
        {
            var encounter = scene.EncounterId is { } encounterId ? await db.Encounters.AsNoTracking().FirstOrDefaultAsync(e => e.Id == encounterId, ct) : null;
            if (encounter is null)
                throw RequestValidationException.For("encounterId", L.T("Vincule um combate à cena primeiro.", "Link a fight to the scene first."));
            var placed = state.Tokens.Where(t => t.CombatantId is not null).Select(t => t.CombatantId!).ToHashSet();
            var tokens = state.Tokens.ToList();
            var columns = new Dictionary<CombatSide, int>();
            foreach (var c in EncounterService.Deserialize(encounter.StateJson).Combatants.Where(c => !placed.Contains(c.Id)))
            {
                if (tokens.Count >= MaxTokens) break;
                var column = columns.GetValueOrDefault(c.Side);
                columns[c.Side] = column + 1;
                tokens.Add(new SceneToken
                {
                    Id = NextId("T", tokens.Select(t => t.Id)), Label = Truncate(c.Name, 40), CombatantId = c.Id,
                    CharacterId = c.CharacterId, X = 1 + column, Y = c.Side == CombatSide.Ally ? 1 : 3,
                    Color = c.Side == CombatSide.Ally ? "#00f0ff" : "#ff2a6d",
                    // On the scene the players already see, an enemy appears only when the GM shows it.
                    Visibility = c.Side == CombatSide.Enemy && scene.Active ? TokenVisibility.Gm : TokenVisibility.All,
                });
            }
            return state with { Tokens = tokens };
        }, bumpVersion: false, ct);

    public Task<SceneView> AddTemplateAsync(int id, TemplateInput input, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => Task.FromResult(AddTemplate(state, input, null)), bumpVersion: false, ct);

    public Task<SceneView> UpdateTemplateAsync(int id, string templateId, TemplateInput input, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            var current = RequireTemplate(state, templateId);
            return Task.FromResult(ReplaceTemplate(state, Template(templateId, input, current.OwnerSeatId)));
        }, bumpVersion: false, ct);

    public Task<SceneView> RemoveTemplateAsync(int id, string templateId, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            RequireTemplate(state, templateId);
            return Task.FromResult(state with { Templates = state.Templates.Where(t => t.Id != templateId).ToList() });
        }, bumpVersion: false, ct);

    // ------------------------------------------------------------------ players

    /// <summary>The active scene as this seat sees it; null when the GM shows none.</summary>
    public async Task<SceneView?> PlayerSceneAsync(TableSeat seat, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct);
        if (scene is null) return null;
        var state = Deserialize(scene.StateJson);
        return SceneViews.ForSeat(scene, state, seat, await PortraitsAsync(state, ct));
    }

    public async Task<SceneView> PlayerMoveTokenAsync(TableSeat seat, string tokenId, TokenMoveRequest move, CancellationToken ct) =>
        await PlayerMutateAsync(seat, (_, state) =>
        {
            var token = state.Tokens.FirstOrDefault(t => t.Id == tokenId && SceneViews.CanSee(t, seat)) ?? throw TokenNotFound();
            if (!SceneViews.CanControl(token, seat))
                throw RequestValidationException.For("tokenId", L.T("Esse token não é seu (ou está travado pelo mestre).", "That token isn't yours (or the GM locked it)."));
            return Move(state, token, move);
        }, ct);

    public Task<SceneView> PlayerAddTemplateAsync(TableSeat seat, TemplateInput input, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) => AddTemplate(state, input with { Locked = false }, seat.Id), ct);

    public Task<SceneView> PlayerUpdateTemplateAsync(TableSeat seat, string templateId, TemplateInput input, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            RequireOwnTemplate(state, templateId, seat);
            return ReplaceTemplate(state, Template(templateId, input with { Locked = false }, seat.Id));
        }, ct);

    public Task<SceneView> PlayerRemoveTemplateAsync(TableSeat seat, string templateId, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            RequireOwnTemplate(state, templateId, seat);
            return state with { Templates = state.Templates.Where(t => t.Id != templateId).ToList() };
        }, ct);

    public Task<bool> IsActiveAsync(int sceneId, CancellationToken ct) => db.Scenes.AnyAsync(s => s.Id == sceneId && s.Active, ct);

    /// <summary>The fight linked to the active scene and the scene's state (for the players' view of it).</summary>
    public async Task<(int? EncounterId, SceneState? State)> ActiveAsync(CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct);
        return scene is null ? (null, null) : (scene.EncounterId, Deserialize(scene.StateJson));
    }

    /// <summary>The character whose portrait a visible token of the active scene shows.</summary>
    public async Task<int> PlayerTokenCharacterAsync(TableSeat seat, string tokenId, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct) ?? throw TokenNotFound();
        var token = Deserialize(scene.StateJson).Tokens.FirstOrDefault(t => t.Id == tokenId && SceneViews.CanSee(t, seat));
        return token?.CharacterId ?? throw TokenNotFound();
    }

    /// <summary>
    /// Whether a live drag preview may be shown: a token of the active scene the seat controls (null seat = the GM, any
    /// token). Null when it may not; otherwise whether every player may see the preview (only public tokens).
    /// </summary>
    public async Task<bool?> DragTargetAsync(TableSeat? seat, int sceneId, string tokenId, CancellationToken ct)
    {
        // Token ids repeat across scenes: a preview counts only for the scene on the players' screens.
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active && s.Id == sceneId, ct);
        var token = scene is null ? null : Deserialize(scene.StateJson).Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null || (seat is not null && !SceneViews.CanControl(token, seat))) return null;
        return token.Visibility == TokenVisibility.All;
    }

    // ------------------------------------------------------------------ plumbing

    private async Task<SceneView> MutateAsync(int id, int? version, Func<Scene, SceneState, Task<SceneState>> operation, bool bumpVersion, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(id, ct);
        var scene = await db.Scenes.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFound();
        if (version is { } v && scene.Version != v)
            throw new ConflictException(L.T("Esta cena foi alterada em outra janela. Recarregue para ver a versão atual.", "This scene was changed in another window. Reload to see the current version."));
        var state = await operation(scene, Deserialize(scene.StateJson));
        scene.StateJson = Serialize(state);
        scene.UpdatedAt = DateTime.UtcNow;
        if (bumpVersion) scene.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(L.T("Esta cena foi alterada ao mesmo tempo em outra janela. Recarregue e tente de novo.", "This scene was changed at the same time in another window. Reload and try again."));
        }
        return await PublishAsync(scene, state, ct);
    }

    /// <summary>A player's change, always on the active scene; answered with the seat's own view.</summary>
    private async Task<SceneView> PlayerMutateAsync(TableSeat seat, Func<Scene, SceneState, SceneState> operation, CancellationToken ct)
    {
        var id = await db.Scenes.Where(s => s.Active).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct)
                 ?? throw new NotFoundException(L.T("O mestre não está mostrando nenhuma cena.", "The GM isn't showing any scene."));
        await MutateAsync(id, null, (scene, state) =>
        {
            if (!scene.Active) throw new NotFoundException(L.T("O mestre trocou de cena.", "The GM switched scenes."));
            return Task.FromResult(operation(scene, state));
        }, bumpVersion: false, ct);
        return await PlayerSceneAsync(seat, ct) ?? throw NotFound();
    }

    /// <summary>Sends the GM view, and each open seat's view when the scene is the one on the players' screens.</summary>
    private async Task<SceneView> PublishAsync(Scene scene, SceneState state, CancellationToken ct)
    {
        var portraits = await PortraitsAsync(state, ct);
        var gm = SceneViews.ForGm(scene, state, portraits);
        Dictionary<int, SceneView?>? seats = null;
        if (scene.Active)
            seats = (await OpenSeatsAsync(ct)).ToDictionary(s => s.Id, s => (SceneView?)SceneViews.ForSeat(scene, state, s, portraits));
        await notifier.SceneAsync(gm, seats, ct);
        return gm;
    }

    private async Task<Dictionary<int, SceneView?>> NoSceneAsync(CancellationToken ct) =>
        (await OpenSeatsAsync(ct)).ToDictionary(s => s.Id, _ => (SceneView?)null);

    private Task<List<TableSeat>> OpenSeatsAsync(CancellationToken ct) =>
        db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);

    private async Task<IReadOnlySet<int>> PortraitsAsync(SceneState state, CancellationToken ct)
    {
        var ids = state.Tokens.Where(t => t.CharacterId is not null && t.ImageMediaId is null).Select(t => t.CharacterId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new HashSet<int>();
        return (await db.CharacterPortraits.AsNoTracking().Where(p => ids.Contains(p.CharacterId)).Select(p => p.CharacterId).ToListAsync(ct)).ToHashSet();
    }

    private static SceneState Move(SceneState state, SceneToken token, TokenMoveRequest move)
    {
        var moved = token with
        {
            X = Cell(move.X, "x"), Y = Cell(move.Y, "y"),
            Rotation = move.Rotation is { } r ? Rotation(r) : token.Rotation,
            Width = move.Width is { } w ? Size(w, "width") : token.Width,
            Height = move.Height is { } h ? Size(h, "height") : token.Height,
        };
        return state with { Tokens = state.Tokens.Select(t => t.Id == token.Id ? moved : t).ToList() };
    }

    private async Task<SceneToken> TokenAsync(string id, TokenInput i, CancellationToken ct)
    {
        if (i.ImageMediaId is { } media) await RequireMediaAsync(media, "imageMediaId", ct);
        if (i.CharacterId is { } characterId && !await db.Characters.AnyAsync(c => c.Id == characterId, ct))
            throw RequestValidationException.For("characterId", L.T("Personagem não encontrado.", "Character not found."));
        var seatIds = (i.VisibleSeatIds ?? []).Distinct().ToList();
        if (i.OwnerSeatId is { } owner) seatIds.Add(owner);
        if (seatIds.Count > 0 && await db.TableSeats.CountAsync(s => seatIds.Contains(s.Id), ct) != seatIds.Distinct().Count())
            throw RequestValidationException.For("ownerSeatId", L.T("Assento não encontrado.", "Seat not found."));
        if (i.CombatantId is { Length: > 20 })
            throw RequestValidationException.For("combatantId", L.T("Combatente inválido.", "Invalid combatant."));
        if (i.RangeRingMeters is { } ring && (!double.IsFinite(ring) || ring is < 0 or > 1000))
            throw RequestValidationException.For("rangeRingMeters", L.T("O anel de alcance vai de 0 a 1000 m.", "The range ring goes from 0 to 1000 m."));
        return new SceneToken
        {
            Id = id, Label = RequireLabel(i.Label), X = Cell(i.X, "x"), Y = Cell(i.Y, "y"), Width = Size(i.Width, "width"),
            Height = Size(i.Height, "height"), Rotation = Rotation(i.Rotation), Locked = i.Locked, ImageMediaId = i.ImageMediaId,
            Color = Color(i.Color, "#00f0ff"), OwnerSeatId = i.OwnerSeatId, Visibility = i.Visibility,
            VisibleSeatIds = i.Visibility == TokenVisibility.Seats ? (i.VisibleSeatIds ?? []).Distinct().ToList() : [],
            CharacterId = i.CharacterId, CombatantId = string.IsNullOrWhiteSpace(i.CombatantId) ? null : i.CombatantId,
            RangeRingMeters = i.RangeRingMeters is > 0 ? i.RangeRingMeters : null,
        };
    }

    private static SceneState AddTemplate(SceneState state, TemplateInput input, int? seatId)
    {
        if (state.Templates.Count >= MaxTemplates)
            throw RequestValidationException.For("templates", L.T($"Uma cena comporta até {MaxTemplates} áreas.", $"A scene holds up to {MaxTemplates} areas."));
        return state with { Templates = [.. state.Templates, Template(NextId("A", state.Templates.Select(t => t.Id)), input, seatId)] };
    }

    private static SceneState ReplaceTemplate(SceneState state, SceneTemplate template) =>
        state with { Templates = state.Templates.Select(t => t.Id == template.Id ? template : t).ToList() };

    private static SceneTemplate Template(string id, TemplateInput i, int? seatId)
    {
        if (!double.IsFinite(i.SizeMeters) || i.SizeMeters is < 0.5 or > 200)
            throw RequestValidationException.For("sizeMeters", L.T("O tamanho da área vai de 0,5 a 200 m.", "The area size goes from 0.5 to 200 m."));
        var label = i.Label?.Trim();
        return new SceneTemplate
        {
            Id = id, Shape = i.Shape, X = Cell(i.X, "x"), Y = Cell(i.Y, "y"), SizeMeters = i.SizeMeters, Angle = Rotation(i.Angle),
            Color = Color(i.Color, "#ff2a6d"), Label = string.IsNullOrEmpty(label) ? null : Truncate(label, 40), Locked = i.Locked, OwnerSeatId = seatId,
        };
    }

    private static SceneGrid Grid(SceneGrid? g)
    {
        g ??= new SceneGrid();
        if (!double.IsFinite(g.SizePx) || g.SizePx is < 8 or > 1000)
            throw RequestValidationException.For("grid.sizePx", L.T("A célula da grade vai de 8 a 1000 pixels.", "A grid cell goes from 8 to 1000 pixels."));
        if (!double.IsFinite(g.MetersPerCell) || g.MetersPerCell is < 0.1 or > 100)
            throw RequestValidationException.For("grid.metersPerCell", L.T("A escala vai de 0,1 a 100 m por célula.", "The scale goes from 0.1 to 100 m per cell."));
        if (!double.IsFinite(g.OffsetX) || !double.IsFinite(g.OffsetY) || Math.Abs(g.OffsetX) > 1000 || Math.Abs(g.OffsetY) > 1000)
            throw RequestValidationException.For("grid.offsetX", L.T("Deslocamento de grade inválido.", "Invalid grid offset."));
        return g with
        {
            Color = Color(g.Color, "#00f0ff"),
            Opacity = double.IsFinite(g.Opacity) ? Math.Clamp(g.Opacity, 0, 1) : 0.35,
        };
    }

    private async Task RequireMediaAsync(Guid id, string field, CancellationToken ct)
    {
        if (!await db.MediaAssets.AnyAsync(m => m.Id == id, ct))
            throw RequestValidationException.For(field, L.T("Imagem não encontrada. Envie de novo.", "Image not found. Upload it again."));
    }

    private static SceneToken RequireToken(SceneState state, string id) => state.Tokens.FirstOrDefault(t => t.Id == id) ?? throw TokenNotFound();

    private static SceneTemplate RequireTemplate(SceneState state, string id) =>
        state.Templates.FirstOrDefault(t => t.Id == id) ?? throw new NotFoundException(L.T("Área não encontrada.", "Area not found."));

    private static void RequireOwnTemplate(SceneState state, string id, TableSeat seat)
    {
        if (RequireTemplate(state, id).OwnerSeatId != seat.Id)
            throw RequestValidationException.For("templateId", L.T("Só dá para mexer nas áreas que você colocou.", "You can only change the areas you placed."));
    }

    private static double Cell(double value, string field) =>
        double.IsFinite(value) && value is >= MinCell and <= MaxCell ? Math.Round(value, 3)
            : throw RequestValidationException.For(field, L.T("Posição fora do mapa.", "Position off the map."));

    private static double Size(double value, string field) =>
        double.IsFinite(value) && value is >= 0.25 and <= 20 ? Math.Round(value, 3)
            : throw RequestValidationException.For(field, L.T("O tamanho vai de 0,25 a 20 células.", "The size goes from 0.25 to 20 cells."));

    private static double Rotation(double value) =>
        double.IsFinite(value) ? Math.Round(((value % 360) + 360) % 360, 2) : 0;

    private static string Color(string? color, string fallback) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit) ? color.ToLowerInvariant() : fallback;

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed.Length is > 0 and <= 80 ? trimmed
            : throw RequestValidationException.For("name", L.T("Dê um nome à cena (até 80 caracteres).", "Name the scene (up to 80 characters)."));
    }

    private static string RequireLabel(string? label)
    {
        var trimmed = label?.Trim() ?? "";
        return trimmed.Length is > 0 and <= 40 ? trimmed
            : throw RequestValidationException.For("label", L.T("Dê um nome ao token (até 40 caracteres).", "Name the token (up to 40 characters)."));
    }

    private static string NextId(string prefix, IEnumerable<string> used)
    {
        var taken = used.ToHashSet();
        return $"{prefix}{Enumerable.Range(1, 99999).First(n => !taken.Contains($"{prefix}{n}"))}";
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    internal static SceneState Deserialize(string json) => JsonSerializer.Deserialize<SceneState>(json, Json) ?? new SceneState();

    private static string Serialize(SceneState state) => JsonSerializer.Serialize(state, Json);

    private static NotFoundException NotFound() => new(L.T("Cena não encontrada.", "Scene not found."));

    private static NotFoundException TokenNotFound() => new(L.T("Token não encontrado.", "Token not found."));
}
