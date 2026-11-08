using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// Scenes of the virtual table. GM edits anything; players only move their own tokens/templates on the active scene.
// Settings edits carry the scene version, tokens and templates are last-write-wins (serialized per scene).
public sealed class SceneService(IAppDbContext db, SceneLocks locks, ITableNotifier notifier, NpcCompendium compendium, TableSessionService sessions)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public const int MaxTokens = 300;
    public const int MaxTemplates = 100;
    public const int MaxImages = 200;
    public const int MaxImagesPerSeat = 10;
    private const double MinCell = -100, MaxCell = 2000;

    // library

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
        return SceneViews.ForGm(scene, state, await ImagesAsync(state, ct), await CoversAsync(scene, state, ct));
    }

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
            // fights linked from the table belong to the open session
            if (r.EncounterId is { } linked) await sessions.StampFightAsync(linked, ct);
            if (r.BackgroundMediaId is { } media) await RequireMediaAsync(media, "backgroundMediaId", ct);
            if (r.BackgroundWidth is < 0 or > 20000 || r.BackgroundHeight is < 0 or > 20000)
                throw RequestValidationException.For("backgroundWidth", L.T("Tamanho de mapa inválido.", "Invalid map size."));
            return state with
            {
                Grid = Grid(r.Grid),
                Background = new SceneBackground { MediaId = r.BackgroundMediaId, Width = r.BackgroundWidth, Height = r.BackgroundHeight },
            };
        }, bumpVersion: true, ct);

    // e.g. the same bar before and after the shootout. Starts inactive.
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

    // tokens and templates (GM)

    public Task<SceneView> AddTokenAsync(int id, TokenInput input, CancellationToken ct) =>
        MutateAsync(id, null, async (_, state) =>
        {
            if (state.Tokens.Count >= MaxTokens)
                throw RequestValidationException.For("tokens", L.T($"Uma cena comporta até {MaxTokens} tokens.", $"A scene holds up to {MaxTokens} tokens."));
            var token = await TokenAsync(NextId("T", state.Tokens.Select(t => t.Id)), input, null, ct);
            return state with { Tokens = [.. state.Tokens, token] };
        }, bumpVersion: false, ct);

    public Task<SceneView> UpdateTokenAsync(int id, string tokenId, TokenInput input, CancellationToken ct) =>
        MutateAsync(id, null, async (_, state) =>
        {
            var token = await TokenAsync(tokenId, input, RequireToken(state, tokenId), ct);
            return state with { Tokens = state.Tokens.Select(t => t.Id == tokenId ? token : t).ToList() };
        }, bumpVersion: false, ct);

    public Task<SceneView> MoveTokenAsync(int id, string tokenId, TokenMoveRequest move, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => Task.FromResult(Move(state, RequireToken(state, tokenId), move)), bumpVersion: false, ct);

    public Task<SceneView> RemoveTokenAsync(int id, string tokenId, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            RequireToken(state, tokenId);
            return Task.FromResult(state with { Tokens = state.Tokens.Where(t => t.Id != tokenId).ToList() });
        }, bumpVersion: false, ct);

    // One token per combatant that doesn't have one yet: allies on the first row, enemies two rows below.
    // Enemy tokens start hidden on a live scene.
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
                    CharacterId = c.CharacterId, NpcId = c.Snapshot?.NpcId, CompendiumNpcId = c.Snapshot?.CompendiumId, X = 1 + column, Y = c.Side == CombatSide.Ally ? 1 : 3,
                    Color = c.Side == CombatSide.Ally ? "#00f0ff" : "#ff2a6d",
                    // enemies stay hidden on the live scene until the GM shows them
                    Visibility = c.Side == CombatSide.Enemy && scene.Active ? TokenVisibility.Gm : TokenVisibility.All,
                });
            }
            return state with { Tokens = tokens };
        }, bumpVersion: false, ct);

    // token id -> combatant id
    public Task<SceneView> LinkFightAsync(int id, int encounterId, IReadOnlyDictionary<string, string> combatants, CancellationToken ct) =>
        MutateAsync(id, null, async (scene, state) =>
        {
            scene.EncounterId = encounterId;
            await sessions.StampFightAsync(encounterId, ct);
            return state with { Tokens = state.Tokens.Select(t => combatants.TryGetValue(t.Id, out var c) ? t with { CombatantId = c } : t).ToList() };
        }, bumpVersion: true, ct);

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

    // players

    // null when the GM isn't showing any scene
    public async Task<SceneView?> PlayerSceneAsync(TableSeat seat, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct);
        if (scene is null) return null;
        var state = Deserialize(scene.StateJson);
        return SceneViews.ForSeat(scene, state, seat, await ImagesAsync(state, ct), await CoversAsync(scene, state, ct));
    }

    public Task<SceneView> BatchAsync(int id, SceneBatchRequest r, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => Task.FromResult(ApplyBatch(state, r, null)), bumpVersion: false, ct);

    // all or nothing
    public Task<SceneView> PlayerBatchAsync(TableSeat seat, SceneBatchRequest r, CancellationToken ct)
    {
        if ((r.RemoveTokens?.Count ?? 0) + (r.HideTokens?.Count ?? 0) + (r.ShowTokens?.Count ?? 0) > 0)
            throw RequestValidationException.For("tokens", L.T("Só o mestre apaga, oculta ou mostra tokens.", "Only the GM clears, hides or shows tokens."));
        return PlayerMutateAsync(seat, (_, state) => ApplyBatch(state, r, seat), ct);
    }

    public async Task<SceneView> PlayerMoveTokenAsync(TableSeat seat, string tokenId, TokenMoveRequest move, CancellationToken ct) =>
        await PlayerMutateAsync(seat, (_, state) =>
        {
            var token = state.Tokens.FirstOrDefault(t => t.Id == tokenId && SceneViews.CanSee(t, seat)) ?? throw TokenNotFound();
            if (!SceneViews.CanControl(token, seat))
                throw RequestValidationException.For("tokenId", L.T("Esse token não é seu (ou está travado pelo mestre).", "That token isn't yours (or the GM locked it)."));
            return Move(state, token, move);
        }, ct);

    // The seat's own token (character portrait, or just the seat name). One per seat and scene.
    public async Task<SceneView> PlayerPlaceOwnTokenAsync(TableSeat seat, PlaceTokenRequest r, CancellationToken ct)
    {
        var name = seat.CharacterId is { } characterId
            ? await db.Characters.AsNoTracking().Where(c => c.Id == characterId).Select(c => c.Handle ?? c.Name).FirstOrDefaultAsync(ct)
            : null;
        var linkedCharacter = name is null ? null : seat.CharacterId;
        string? combatantId = null;
        if (linkedCharacter is { } pc && await db.Scenes.Where(s => s.Active).Select(s => s.EncounterId).FirstOrDefaultAsync(ct) is { } encounterId
            && await db.Encounters.AsNoTracking().Where(e => e.Id == encounterId).Select(e => e.StateJson).FirstOrDefaultAsync(ct) is { } fight)
            combatantId = EncounterService.Deserialize(fight).Combatants.FirstOrDefault(c => c.CharacterId == pc)?.Id;
        return await PlayerMutateAsync(seat, (_, state) =>
        {
            if (state.Tokens.Any(t => linkedCharacter is { } c ? t.CharacterId == c : t.OwnerSeatId == seat.Id))
                throw new ConflictException(L.T("Você já tem um token nesta cena.", "You already have a token on this scene."));
            if (state.Tokens.Count >= MaxTokens)
                throw RequestValidationException.For("tokens", L.T($"Uma cena comporta até {MaxTokens} tokens.", $"A scene holds up to {MaxTokens} tokens."));
            var token = new SceneToken
            {
                Id = NextId("T", state.Tokens.Select(t => t.Id)), Label = Truncate(name ?? seat.Label, 40), X = Cell(r.X, "x"), Y = Cell(r.Y, "y"),
                Color = seat.Color, OwnerSeatId = seat.Id, CharacterId = linkedCharacter, CombatantId = combatantId,
            };
            return state with { Tokens = [.. state.Tokens, token] };
        }, ct);
    }

    public Task<SceneView> PlayerAddTemplateAsync(TableSeat seat, TemplateInput input, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) => AddTemplate(state, input with { Locked = false }, seat.Id), ct);

    public Task<SceneView> PlayerUpdateTemplateAsync(TableSeat seat, string templateId, TemplateInput input, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            // the area keeps whoever placed it, no matter who moves it
            var owner = RequirePlayerTemplate(state, templateId).OwnerSeatId;
            return ReplaceTemplate(state, Template(templateId, input with { Locked = false }, owner));
        }, ct);

    public Task<SceneView> PlayerRemoveTemplateAsync(TableSeat seat, string templateId, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            RequirePlayerTemplate(state, templateId);
            return state with { Templates = state.Templates.Where(t => t.Id != templateId).ToList() };
        }, ct);

    // images over the map

    public Task<SceneView> AddImageAsync(int id, ImageInput input, CancellationToken ct) =>
        MutateAsync(id, null, async (_, state) => AddImage(state, input, null, await RequireImageMediaAsync(input, ct)), bumpVersion: false, ct);

    public Task<SceneView> UpdateImageAsync(int id, string imageId, ImageInput input, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            var current = RequireImage(state, imageId, null);
            // Only the GM toggles the GM lock. Hiding a player's locked image doesn't make the lock the GM's.
            return Task.FromResult(ReplaceImage(state, Changed(current, input) with
            {
                Locked = input.Locked, LockedByGm = input.Locked && (!current.Locked || current.LockedByGm), Hidden = input.Hidden,
            }));
        }, bumpVersion: false, ct);

    public Task<SceneView> OrderImageAsync(int id, string imageId, ImageOrderRequest r, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => { RequireImage(state, imageId, null); return Task.FromResult(Reorder(state, imageId, r.Above)); }, bumpVersion: false, ct);

    public Task<SceneView> RemoveImageAsync(int id, string imageId, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => { RequireImage(state, imageId, null); return Task.FromResult(WithoutImage(state, imageId)); }, bumpVersion: false, ct);

    public async Task<SceneView> PlayerAddImageAsync(TableSeat seat, ImageInput input, CancellationToken ct)
    {
        var media = await RequireImageMediaAsync(input, ct);
        return await PlayerMutateAsync(seat, (_, state) =>
        {
            if (state.Images.Count(i => i.OwnerSeatId == seat.Id) >= MaxImagesPerSeat)
                throw RequestValidationException.For("images", L.T($"Cada jogador põe até {MaxImagesPerSeat} imagens por cena.", $"Each player places up to {MaxImagesPerSeat} images per scene."));
            return AddImage(state, input with { Hidden = false }, seat.Id, media);
        }, ct);
    }

    public Task<SceneView> PlayerUpdateImageAsync(TableSeat seat, string imageId, ImageInput input, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            var current = RequireOwnImage(state, imageId, seat);
            if (current.LockedByGm) throw new ForbiddenException(L.T("O mestre travou esta imagem.", "The GM locked this image."));
            // locked by the player: the only allowed change is unlocking
            if (current.Locked && !input.Locked)
                return ReplaceImage(state, current with { Locked = false });
            if (current.Locked) throw new ForbiddenException(L.T("Destrave a imagem antes de mexer nela.", "Unlock the image before changing it."));
            return ReplaceImage(state, Changed(current, input) with { Locked = input.Locked, LockedByGm = false });
        }, ct);

    public Task<SceneView> PlayerOrderImageAsync(TableSeat seat, string imageId, ImageOrderRequest r, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            var current = RequireOwnImage(state, imageId, seat);
            if (current.Locked) throw new ForbiddenException(L.T("Destrave a imagem antes de mexer nela.", "Unlock the image before changing it."));
            if (r.Above is { } above && !state.Images.Any(i => i.Id == above && !i.Hidden))
                throw new NotFoundException(L.T("Imagem não encontrada.", "Image not found."));
            return Reorder(state, imageId, r.Above);
        }, ct);

    public Task<SceneView> PlayerRemoveImageAsync(TableSeat seat, string imageId, CancellationToken ct) =>
        PlayerMutateAsync(seat, (_, state) =>
        {
            var current = RequireOwnImage(state, imageId, seat);
            if (current.Locked) throw new ForbiddenException(L.T("Destrave a imagem antes de apagá-la.", "Unlock the image before deleting it."));
            return WithoutImage(state, imageId);
        }, ct);

    private async Task<Guid> RequireImageMediaAsync(ImageInput input, CancellationToken ct)
    {
        if (input.MediaId is not { } media) throw RequestValidationException.For("mediaId", L.T("Envie a imagem primeiro.", "Upload the image first."));
        await RequireMediaAsync(media, "mediaId", ct);
        return media;
    }

    private static SceneState AddImage(SceneState state, ImageInput input, int? seatId, Guid media)
    {
        if (state.Images.Count >= MaxImages)
            throw RequestValidationException.For("images", L.T($"Uma cena comporta até {MaxImages} imagens.", $"A scene holds up to {MaxImages} images."));
        var image = Changed(new SceneImage { Id = NextId("I", state.Images.Select(i => i.Id)), MediaId = media, OwnerSeatId = seatId, Width = 4, Height = 4 }, input)
            with { Order = state.Images.Count, Hidden = seatId is null && input.Hidden, Locked = input.Locked, LockedByGm = seatId is null && input.Locked };
        return state with { Images = [.. state.Images, image] };
    }

    // size 0.25-200 cells, opacity 0.1-1, name up to 60
    private static SceneImage Changed(SceneImage current, ImageInput i)
    {
        var width = i.Width ?? current.Width;
        var height = i.Height ?? current.Height;
        if (!double.IsFinite(i.X) || !double.IsFinite(i.Y) || Math.Abs(i.X) > 5000 || Math.Abs(i.Y) > 5000)
            throw RequestValidationException.For("x", L.T("Posição inválida.", "Invalid position."));
        if (!double.IsFinite(width) || !double.IsFinite(height) || width is < 0.25 or > 200 || height is < 0.25 or > 200)
            throw RequestValidationException.For("width", L.T("O tamanho da imagem vai de 0,25 a 200 células.", "The image size goes from 0.25 to 200 cells."));
        if (!double.IsFinite(i.Opacity) || i.Opacity is < 0.1 or > 1)
            throw RequestValidationException.For("opacity", L.T("A opacidade vai de 10% a 100%.", "Opacity goes from 10% to 100%."));
        if (!double.IsFinite(i.Rotation)) throw RequestValidationException.For("rotation", L.T("Giro inválido.", "Invalid rotation."));
        var name = string.IsNullOrWhiteSpace(i.Name) ? (current.Name.Length > 0 ? current.Name : L.T("Imagem", "Image")) : i.Name.Trim();
        return current with
        {
            Name = name.Length <= 60 ? name : name[..60], X = i.X, Y = i.Y, Width = width, Height = height,
            Rotation = ((i.Rotation % 360) + 360) % 360, Opacity = i.Opacity,
        };
    }

    private static SceneImage RequireImage(SceneState state, string imageId, TableSeat? seat) =>
        state.Images.FirstOrDefault(i => i.Id == imageId && (seat is null || !i.Hidden))
        ?? throw new NotFoundException(L.T("Imagem não encontrada.", "Image not found."));

    // hidden images don't exist for players
    private static SceneImage RequireOwnImage(SceneState state, string imageId, TableSeat seat)
    {
        var image = RequireImage(state, imageId, seat);
        return image.OwnerSeatId == seat.Id ? image : throw new ForbiddenException(L.T("Só quem pôs a imagem (ou o mestre) mexe nela.", "Only who placed the image (or the GM) can change it."));
    }

    private static SceneState ReplaceImage(SceneState state, SceneImage image) =>
        state with { Images = state.Images.Select(i => i.Id == image.Id ? image : i).ToList() };

    private static SceneState WithoutImage(SceneState state, string imageId) =>
        Renumber(state with { Images = state.Images.Where(i => i.Id != imageId).ToList() }, state.Images.Where(i => i.Id != imageId).OrderBy(i => i.Order).Select(i => i.Id).ToList());

    // above == null moves it to the top
    private static SceneState Reorder(SceneState state, string imageId, string? above)
    {
        var stack = state.Images.OrderBy(i => i.Order).Select(i => i.Id).Where(id => id != imageId).ToList();
        var at = above is null ? stack.Count : stack.IndexOf(above);
        if (at < 0) throw new NotFoundException(L.T("Imagem não encontrada.", "Image not found."));
        stack.Insert(at, imageId);
        return Renumber(state, stack);
    }

    private static SceneState Renumber(SceneState state, List<string> bottomToTop) =>
        state with { Images = state.Images.Select(i => i with { Order = bottomToTop.IndexOf(i.Id) }).ToList() };

    public async Task RepublishTokensOfAsync(Func<SceneToken, bool> shows, CancellationToken ct)
    {
        foreach (var scene in await db.Scenes.AsNoTracking().ToListAsync(ct))
        {
            var state = Deserialize(scene.StateJson);
            if (state.Tokens.Any(shows)) await PublishAsync(scene, state, ct);
        }
    }

    public Task<bool> IsActiveAsync(int sceneId, CancellationToken ct) => db.Scenes.AnyAsync(s => s.Id == sceneId && s.Active, ct);

    public async Task<(int? EncounterId, SceneState? State)> ActiveAsync(CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct);
        return scene is null ? (null, null) : (scene.EncounterId, Deserialize(scene.StateJson));
    }

    public async Task<TokenImageSource> PlayerTokenImageAsync(TableSeat seat, string tokenId, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct) ?? throw TokenNotFound();
        var state = Deserialize(scene.StateJson);
        var token = state.Tokens.FirstOrDefault(t => t.Id == tokenId && SceneViews.CanSee(t, seat)) ?? throw TokenNotFound();
        return SceneViews.ImageSource(token, await ImagesAsync(state with { Tokens = [token] }, ct)) ?? throw TokenNotFound();
    }

    // null = not allowed. Otherwise: can every player see the preview? (public tokens only)
    public async Task<bool?> DragTargetAsync(TableSeat? seat, int sceneId, string tokenId, CancellationToken ct)
    {
        // token ids repeat across scenes
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active && s.Id == sceneId, ct);
        var token = scene is null ? null : Deserialize(scene.StateJson).Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null || (seat is not null && !SceneViews.CanControl(token, seat))) return null;
        return token.Visibility == TokenVisibility.All;
    }

    // null = can't move it, false = GM only (hidden), true = everyone
    public async Task<bool?> ImageDragTargetAsync(TableSeat? seat, int sceneId, string imageId, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active && s.Id == sceneId, ct);
        var image = scene is null ? null : Deserialize(scene.StateJson).Images.FirstOrDefault(i => i.Id == imageId);
        if (image is null || (seat is not null && (image.OwnerSeatId != seat.Id || image.Locked || image.Hidden))) return null;
        return !image.Hidden;
    }

    // plumbing

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

    // GM view, plus each seat's view if this is the scene on the players' screens
    private async Task<SceneView> PublishAsync(Scene scene, SceneState state, CancellationToken ct)
    {
        var images = await ImagesAsync(state, ct);
        var covers = await CoversAsync(scene, state, ct);
        var gm = SceneViews.ForGm(scene, state, images, covers);
        Dictionary<int, SceneView?>? seats = null;
        if (scene.Active)
            seats = (await OpenSeatsAsync(ct)).ToDictionary(s => s.Id, s => (SceneView?)SceneViews.ForSeat(scene, state, s, images, covers));
        await notifier.SceneAsync(gm, seats, ct);
        return gm;
    }

    // only read when one of the barriers is a cover
    private async Task<IReadOnlyDictionary<string, int>?> CoversAsync(Scene scene, SceneState state, CancellationToken ct)
    {
        if (scene.EncounterId is not { } encounterId || !state.Tokens.Any(t => t.Barrier?.CoverId is not null)) return null;
        var json = await db.Encounters.AsNoTracking().Where(e => e.Id == encounterId).Select(e => e.StateJson).FirstOrDefaultAsync(ct);
        return json is null ? null : EncounterService.Deserialize(json).Covers.ToDictionary(c => c.Id, c => c.Hp);
    }

    public async Task RepublishCoversOfAsync(int encounterId, CancellationToken ct)
    {
        foreach (var scene in await db.Scenes.AsNoTracking().Where(s => s.EncounterId == encounterId).ToListAsync(ct))
        {
            var state = Deserialize(scene.StateJson);
            if (state.Tokens.Any(t => t.Barrier?.CoverId is not null)) await PublishAsync(scene, state, ct);
        }
    }

    // barriers

    public Task<SceneView> AddBarrierAsync(int id, BarrierRequest r, int hp, string? coverId, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            if (state.Tokens.Count >= MaxTokens)
                throw RequestValidationException.For("tokens", L.T($"Uma cena comporta até {MaxTokens} tokens.", $"A scene holds up to {MaxTokens} tokens."));
            var token = new SceneToken
            {
                Id = NextId("T", state.Tokens.Select(t => t.Id)),
                Label = string.IsNullOrWhiteSpace(r.Label) ? L.T("Barreira", "Barrier") : Truncate(r.Label.Trim(), 40),
                X = Cell(r.X, "x"), Y = Cell(r.Y, "y"), Width = Size(r.Width, "width"), Height = Size(r.Height, "height"),
                Color = BarrierColor(r.Material), Layer = TokenLayer.Scenery, Locked = false,
                Barrier = new SceneBarrier { Material = r.Material, Thick = r.Thick, MaxHp = hp, Hp = hp, CoverId = coverId },
            };
            return Task.FromResult(state with { Tokens = [.. state.Tokens, token] });
        }, bumpVersion: false, ct);

    // only for barriers that aren't covers of a fight
    public Task<SceneView> SetBarrierHpAsync(int id, string tokenId, int delta, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) =>
        {
            var token = RequireToken(state, tokenId);
            var b = token.Barrier ?? throw RequestValidationException.For("tokenId", L.T("Esse token não é uma barreira.", "That token isn't a barrier."));
            var next = token with { Barrier = b with { Hp = Math.Clamp(b.Hp - delta, 0, b.MaxHp) } };
            return Task.FromResult(state with { Tokens = state.Tokens.Select(t => t.Id == tokenId ? next : t).ToList() });
        }, bumpVersion: false, ct);

    // token id -> cover id, done when the fight starts
    public Task<SceneView> LinkBarriersAsync(int id, IReadOnlyDictionary<string, string> covers, CancellationToken ct) =>
        MutateAsync(id, null, (_, state) => Task.FromResult(state with
        {
            Tokens = state.Tokens.Select(t => t.Barrier is { } b && covers.TryGetValue(t.Id, out var c) ? t with { Barrier = b with { CoverId = c } } : t).ToList(),
        }), bumpVersion: false, ct);

    private static string BarrierColor(string material) => material switch
    {
        "steel" => "#8a96a3",
        "stone" => "#9b8f7e",
        "bulletproof-glass" => "#7fd4e8",
        "concrete" => "#a3a3a3",
        "wood" => "#a8743f",
        _ => "#d8d0c0",
    };

    private async Task<Dictionary<int, SceneView?>> NoSceneAsync(CancellationToken ct) =>
        (await OpenSeatsAsync(ct)).ToDictionary(s => s.Id, _ => (SceneView?)null);

    private Task<List<TableSeat>> OpenSeatsAsync(CancellationToken ct) =>
        db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);

    // Just the dates. This runs on every scene change and the images can be 2 MB each.
    private async Task<TokenImages> ImagesAsync(SceneState state, CancellationToken ct)
    {
        var tokens = state.Tokens.Where(t => t.ImageMediaId is null).ToList();
        var characters = tokens.Where(t => t.CharacterId is not null).Select(t => t.CharacterId!.Value).Distinct().ToList();
        var npcs = tokens.Where(t => t.NpcId is not null).Select(t => t.NpcId!.Value).Distinct().ToList();
        var book = tokens.Where(t => t.CompendiumNpcId is not null).Select(t => t.CompendiumNpcId!).Distinct().ToList();
        if (characters.Count + npcs.Count + book.Count == 0) return TokenImages.None;
        var focus = new Dictionary<int, CyberpunkDb.Application.Characters.PortraitFocus>();
        if (characters.Count > 0)
        {
            var sheets = await db.Characters.AsNoTracking().Where(c => characters.Contains(c.Id)).Select(c => new { c.Id, c.SheetJson }).ToListAsync(ct);
            foreach (var row in sheets)
                if (CyberpunkDb.Application.Characters.CharacterService.PortraitFocusOf(row.SheetJson) is { } f) focus[row.Id] = f;
        }
        return new TokenImages(
            characters.Count == 0 ? TokenImages.None.Characters
                : await db.CharacterPortraits.AsNoTracking().Where(p => characters.Contains(p.CharacterId)).Select(p => new { p.CharacterId, p.UpdatedAt }).ToDictionaryAsync(p => p.CharacterId, p => p.UpdatedAt, ct),
            npcs.Count == 0 ? TokenImages.None.Npcs
                : await db.NpcPortraits.AsNoTracking().Where(p => npcs.Contains(p.NpcId)).Select(p => new { p.NpcId, p.UpdatedAt }).ToDictionaryAsync(p => p.NpcId, p => p.UpdatedAt, ct),
            book.Count == 0 ? TokenImages.None.Compendium
                : await db.CompendiumPortraits.AsNoTracking().Where(p => book.Contains(p.CompendiumId)).Select(p => new { p.CompendiumId, p.UpdatedAt }).ToDictionaryAsync(p => p.CompendiumId, p => p.UpdatedAt, ct),
            focus);
    }

    // seat == null is the GM. Anything not allowed rejects the whole batch.
    private static SceneState ApplyBatch(SceneState state, SceneBatchRequest r, TableSeat? seat)
    {
        var count = (r.MoveTokens?.Count ?? 0) + (r.MoveTemplates?.Count ?? 0) + (r.RemoveTokens?.Count ?? 0) + (r.RemoveTemplates?.Count ?? 0)
                    + (r.HideTokens?.Count ?? 0) + (r.ShowTokens?.Count ?? 0);
        if (count == 0) throw RequestValidationException.For("batch", L.T("Nada para mudar.", "Nothing to change."));
        if (count > MaxTokens + MaxTemplates) throw RequestValidationException.For("batch", L.T("Seleção grande demais.", "Selection too large."));

        var tokens = state.Tokens.ToList();
        int TokenIndex(string id)
        {
            var i = tokens.FindIndex(t => t.Id == id);
            if (i < 0 || (seat is not null && !SceneViews.CanSee(tokens[i], seat))) throw TokenNotFound();
            return i;
        }
        foreach (var m in r.MoveTokens ?? [])
        {
            var i = TokenIndex(m.Id);
            if (seat is not null && !SceneViews.CanControl(tokens[i], seat))
                throw RequestValidationException.For("tokens", L.T("Esse token não é seu (ou está travado pelo mestre).", "That token isn't yours (or the GM locked it)."));
            tokens[i] = tokens[i] with { X = Cell(m.X, "x"), Y = Cell(m.Y, "y") };
        }
        foreach (var id in r.HideTokens ?? []) { var i = TokenIndex(id); tokens[i] = tokens[i] with { Visibility = TokenVisibility.Gm }; }
        foreach (var id in r.ShowTokens ?? []) { var i = TokenIndex(id); tokens[i] = tokens[i] with { Visibility = TokenVisibility.All }; }
        foreach (var id in r.RemoveTokens ?? []) tokens.RemoveAt(TokenIndex(id));

        var templates = state.Templates.ToList();
        int TemplateIndex(string id)
        {
            if (seat is null) RequireTemplate(state, id); else RequirePlayerTemplate(state, id);
            return templates.FindIndex(t => t.Id == id);
        }
        foreach (var m in r.MoveTemplates ?? [])
        {
            var i = TemplateIndex(m.Id);
            if (i >= 0) templates[i] = templates[i] with { X = Cell(m.X, "x"), Y = Cell(m.Y, "y") };
        }
        foreach (var id in r.RemoveTemplates ?? [])
        {
            var i = TemplateIndex(id);
            if (i >= 0) templates.RemoveAt(i);
        }
        return state with { Tokens = tokens, Templates = templates };
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

    // NPC links are only checked when they change, so a token whose NPC got deleted can still be edited.
    private async Task<SceneToken> TokenAsync(string id, TokenInput i, SceneToken? existing, CancellationToken ct)
    {
        var compendiumNpcId = string.IsNullOrWhiteSpace(i.CompendiumNpcId) ? null : i.CompendiumNpcId;
        if ((i.CharacterId is null ? 0 : 1) + (i.NpcId is null ? 0 : 1) + (compendiumNpcId is null ? 0 : 1) > 1)
            throw RequestValidationException.For("source", L.T("Um token representa um só personagem ou NPC.", "A token stands for a single character or NPC."));
        if (i.ImageMediaId is { } media) await RequireMediaAsync(media, "imageMediaId", ct);
        if (i.CharacterId is { } characterId && !await db.Characters.AnyAsync(c => c.Id == characterId, ct))
            throw RequestValidationException.For("characterId", L.T("Personagem não encontrado.", "Character not found."));
        if (i.NpcId is { } npcId && npcId != existing?.NpcId && !await db.Npcs.AnyAsync(n => n.Id == npcId, ct))
            throw RequestValidationException.For("npcId", L.T("NPC não encontrado.", "NPC not found."));
        if (compendiumNpcId is not null && compendiumNpcId != existing?.CompendiumNpcId && compendium.Find(compendiumNpcId) is null)
            throw RequestValidationException.For("compendiumNpcId", L.T("NPC não encontrado no compêndio.", "NPC not found in the compendium."));
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
            CharacterId = i.CharacterId, NpcId = i.NpcId, CompendiumNpcId = compendiumNpcId,
            CombatantId = string.IsNullOrWhiteSpace(i.CombatantId) ? null : i.CombatantId,
            RangeRingMeters = i.RangeRingMeters is > 0 ? i.RangeRingMeters : null,
            Barrier = existing?.Barrier,
            Layer = existing?.Barrier is not null ? TokenLayer.Scenery : i.Layer,
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

    // anyone can clear a player's area (the grenade went off); GM areas are GM-only
    private static SceneTemplate RequirePlayerTemplate(SceneState state, string id)
    {
        var template = RequireTemplate(state, id);
        if (template.OwnerSeatId is null)
            throw RequestValidationException.For("templateId", L.T("As áreas do mestre só o mestre mexe.", "Only the GM changes the GM's areas."));
        return template;
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

    public static SceneState Deserialize(string json) => JsonSerializer.Deserialize<SceneState>(json, Json) ?? new SceneState();

    private static string Serialize(SceneState state) => JsonSerializer.Serialize(state, Json);

    private static NotFoundException NotFound() => new(L.T("Cena não encontrada.", "Scene not found."));

    private static NotFoundException TokenNotFound() => new(L.T("Token não encontrado.", "Token not found."));
}
