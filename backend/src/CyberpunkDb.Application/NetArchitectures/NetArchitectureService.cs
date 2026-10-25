using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NetArchitectures.Generation;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>
/// Saved NET Architectures: generation, the GM's edits and the export. The cost and the Watchers are recalculated on
/// every read; every write carries the Architecture version (409 on a stale one).
/// </summary>
public sealed class NetArchitectureService(
    IAppDbContext db,
    NetArchitectureRuleBook rules,
    NetIceCatalogProvider catalogs,
    NetArchitectureGenerator generator,
    NetArchitectureOperations operations,
    NetArchitecturePricing pricing,
    NetArchitectureExport export)
{
    internal static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<NetArchitectureRulesDto> GetRulesAsync(CancellationToken ct) =>
        new(rules.Document, (await catalogs.GetAsync(ct)).All.OrderBy(i => i.Price).ThenBy(i => i.Name).ToList());

    public async Task<IReadOnlyList<NetArchitectureSummaryDto>> ListAsync(CancellationToken ct)
    {
        var ice = await catalogs.GetAsync(ct);
        var rows = await db.NetArchitectures.AsNoTracking().OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);
        return rows.Select(a =>
        {
            var state = Deserialize(a.StateJson);
            var quote = pricing.Quote(state, ice);
            return new NetArchitectureSummaryDto(a.Id, a.Name, a.Difficulty, a.Floors, state.Branches.Count - 1, Location(state.LocationId),
                quote.Total, quote.Portable, a.UpdatedAt);
        }).ToList();
    }

    public async Task<NetArchitectureDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        return Detail(entity, Deserialize(entity.StateJson), await catalogs.GetAsync(ct));
    }

    public async Task<NetArchitectureDetailDto> CreateAsync(GenerateArchitectureRequest request, CancellationToken ct)
    {
        var state = generator.Generate(request);
        var now = DateTime.UtcNow;
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{rules.Location(state.LocationId).Label} — {rules.Difficulty(state.Difficulty).Label}, {state.FloorCount} andares"
            : request.Name.Trim();
        var entity = new NetArchitecture { Name = Truncate(name, 80), CreatedAt = now };
        Apply(entity, state, now);
        db.NetArchitectures.Add(entity);
        await db.SaveChangesAsync(ct);
        return Detail(entity, state, await catalogs.GetAsync(ct));
    }

    /// <summary>
    /// With <c>KeepShape</c>, rolls every unlocked floor again on the same floors and branches; otherwise builds a whole
    /// new Architecture with the same difficulty, location and options (and a new seed).
    /// </summary>
    public Task<NetArchitectureDetailDto> RegenerateAsync(int id, RegenerateArchitectureRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (state, _) =>
        {
            var seed = Random.Shared.Next(1, int.MaxValue);
            if (r.KeepShape)
            {
                var kept = state with { Seed = seed };
                generator.Refill(kept, new Random(seed));
                return kept;
            }
            return generator.Generate(new GenerateArchitectureRequest
            {
                Difficulty = state.Difficulty, LocationId = state.LocationId, RespectNodeLimit = state.RespectNodeLimit,
                EnemyNetrunner = state.Netrunners.Count > 0, Seed = seed,
            }) with { Notes = state.Notes };
        }, ct);

    public Task<NetArchitectureDetailDto> UpdateAsync(int id, UpdateArchitectureRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw RequestValidationException.For("name", "Dê um nome à Arquitetura.");
        return MutateAsync(id, r.Version, (state, entity) =>
        {
            entity.Name = Truncate(r.Name.Trim(), 80);
            return state with { Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : Truncate(r.Notes.Trim(), 4000) };
        }, ct);
    }

    public Task<NetArchitectureDetailDto> RerollFloorAsync(int id, string floorId, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.RerollFloor(s, floorId, new Random()); return s; }, ct);

    public Task<NetArchitectureDetailDto> SetFloorAsync(int id, string floorId, SetFloorRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.SetFloor(s, floorId, r.Table, r.Roll, new Random()); return s; }, ct);

    public Task<NetArchitectureDetailDto> LockFloorAsync(int id, string floorId, LockFloorRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.LockFloor(s, floorId, r.Locked); return s; }, ct);

    public async Task<NetArchitectureDetailDto> SwapIceAsync(int id, string contentId, SwapIceRequest r, CancellationToken ct)
    {
        var ice = await catalogs.GetAsync(ct);
        return await MutateAsync(id, r.Version, (s, _) => { operations.SwapIce(s, contentId, r.Slug, ice); return s; }, ct);
    }

    public Task<NetArchitectureDetailDto> SetNodeAsync(int id, string contentId, SetNodeRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.SetNode(s, contentId, r); return s; }, ct);

    public Task<NetArchitectureDetailDto> AddDemonAsync(int id, DemonRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.AddDemon(s, r); return s; }, ct);

    public Task<NetArchitectureDetailDto> UpdateDemonAsync(int id, string demonId, DemonRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.UpdateDemon(s, demonId, r); return s; }, ct);

    public Task<NetArchitectureDetailDto> RemoveDemonAsync(int id, string demonId, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.RemoveDemon(s, demonId); return s; }, ct);

    public Task<NetArchitectureDetailDto> AddNetrunnerAsync(int id, NetrunnerRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.AddNetrunner(s, r); return s; }, ct);

    public Task<NetArchitectureDetailDto> UpdateNetrunnerAsync(int id, string netrunnerId, NetrunnerRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.UpdateNetrunner(s, netrunnerId, r); return s; }, ct);

    public Task<NetArchitectureDetailDto> RemoveNetrunnerAsync(int id, string netrunnerId, VersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _) => { operations.RemoveNetrunner(s, netrunnerId); return s; }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        db.NetArchitectures.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task<(string FileName, string Content)> ExportAsync(int id, bool markdown, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        var state = Deserialize(entity.StateJson);
        var ice = await catalogs.GetAsync(ct);
        var text = export.Render(entity.Name, state, pricing.Quote(state, ice), ice, markdown);
        var file = string.Concat(entity.Name.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return ($"{(file.Length > 0 ? file : "arquitetura")}.{(markdown ? "md" : "txt")}", text);
    }

    // ------------------------------------------------------------------ plumbing

    private async Task<NetArchitectureDetailDto> MutateAsync(int id, int version,
        Func<ArchitectureState, NetArchitecture, ArchitectureState> operation, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        if (entity.Version != version)
            throw new ConflictException("Esta Arquitetura foi alterada em outra janela. Recarregue para ver a versão atual.");
        var state = operation(Deserialize(entity.StateJson), entity);
        Apply(entity, state, DateTime.UtcNow);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Esta Arquitetura foi alterada ao mesmo tempo em outra janela. Recarregue e tente de novo.");
        }
        return Detail(entity, state, await catalogs.GetAsync(ct));
    }

    private static void Apply(NetArchitecture entity, ArchitectureState state, DateTime now)
    {
        entity.Difficulty = state.Difficulty;
        entity.Floors = state.FloorCount;
        entity.LocationId = state.LocationId;
        entity.StateJson = JsonSerializer.Serialize(state, StateJson);
        entity.UpdatedAt = now;
        entity.Version++;
    }

    private NetArchitectureDetailDto Detail(NetArchitecture entity, ArchitectureState state, NetIceCatalog ice)
    {
        var used = state.Contents.Where(c => c.Slug is not null).Select(c => c.Slug!).Distinct()
            .Select(ice.Find).OfType<IceInfo>().ToList();
        return new NetArchitectureDetailDto(entity.Id, entity.Name, entity.Version, entity.CreatedAt, entity.UpdatedAt, state,
            rules.Difficulty(state.Difficulty), Location(state.LocationId), state.Main.Floors[^1].Id, pricing.Quote(state, ice),
            used, export.Watchers(state));
    }

    private LocationSummary Location(string id)
    {
        var location = rules.Locations.FirstOrDefault(l => l.Id == id);
        return new LocationSummary(id, location?.Label ?? id);
    }

    private static ArchitectureState Deserialize(string json) =>
        JsonSerializer.Deserialize<ArchitectureState>(json, StateJson) ?? new ArchitectureState();

    private async Task<NetArchitecture> FindAsync(int id, CancellationToken ct) =>
        await db.NetArchitectures.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException($"Arquitetura {id} não encontrada.");

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
