using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Npcs;

/// <summary>
/// Images of saved NPCs and of the compendium's named NPCs, with the same rules as a character's portrait. The
/// virtual table's tokens of that NPC show it, so a change goes out to the scenes that use it.
/// </summary>
public sealed class NpcPortraitService(IAppDbContext db, NpcCompendium compendium, SceneService scenes)
{
    // ------------------------------------------------------------------ saved NPCs

    public async Task<IReadOnlyDictionary<int, DateTime>> SavedStampsAsync(CancellationToken ct) =>
        await db.NpcPortraits.AsNoTracking().Select(p => new { p.NpcId, p.UpdatedAt }).ToDictionaryAsync(p => p.NpcId, p => p.UpdatedAt, ct);

    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> GetSavedAsync(int npcId, CancellationToken ct)
    {
        var p = await db.NpcPortraits.AsNoTracking().FirstOrDefaultAsync(x => x.NpcId == npcId, ct) ?? throw NoImage();
        return (p.Data, p.ContentType, p.UpdatedAt);
    }

    public async Task SetSavedAsync(int npcId, string contentType, Stream content, long length, CancellationToken ct)
    {
        CharacterService.RequirePortraitFile(contentType, length);
        if (!await db.Npcs.AnyAsync(n => n.Id == npcId, ct))
            throw new NotFoundException(L.T($"NPC {npcId} não encontrado.", $"NPC {npcId} not found."));
        var data = await ReadAsync(content, ct);
        var p = await db.NpcPortraits.FirstOrDefaultAsync(x => x.NpcId == npcId, ct);
        if (p is null) db.NpcPortraits.Add(p = new NpcPortrait { NpcId = npcId });
        p.ContentType = contentType;
        p.Data = data;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await scenes.RepublishTokensOfAsync(t => t.NpcId == npcId, ct);
    }

    public async Task DeleteSavedAsync(int npcId, CancellationToken ct)
    {
        var p = await db.NpcPortraits.FirstOrDefaultAsync(x => x.NpcId == npcId, ct);
        if (p is null) return;
        db.NpcPortraits.Remove(p);
        await db.SaveChangesAsync(ct);
        await scenes.RepublishTokensOfAsync(t => t.NpcId == npcId, ct);
    }

    // ------------------------------------------------------------------ compendium

    public async Task<IReadOnlyDictionary<string, DateTime>> CompendiumStampsAsync(CancellationToken ct) =>
        await db.CompendiumPortraits.AsNoTracking().Select(p => new { p.CompendiumId, p.UpdatedAt }).ToDictionaryAsync(p => p.CompendiumId, p => p.UpdatedAt, ct);

    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> GetCompendiumAsync(string id, CancellationToken ct)
    {
        var p = await db.CompendiumPortraits.AsNoTracking().FirstOrDefaultAsync(x => x.CompendiumId == id, ct) ?? throw NoImage();
        return (p.Data, p.ContentType, p.UpdatedAt);
    }

    public async Task SetCompendiumAsync(string id, string contentType, Stream content, long length, CancellationToken ct)
    {
        CharacterService.RequirePortraitFile(contentType, length);
        compendium.Require(id);
        var data = await ReadAsync(content, ct);
        var p = await db.CompendiumPortraits.FirstOrDefaultAsync(x => x.CompendiumId == id, ct);
        if (p is null) db.CompendiumPortraits.Add(p = new CompendiumPortrait { CompendiumId = id });
        p.ContentType = contentType;
        p.Data = data;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await scenes.RepublishTokensOfAsync(t => t.CompendiumNpcId == id, ct);
    }

    public async Task DeleteCompendiumAsync(string id, CancellationToken ct)
    {
        var p = await db.CompendiumPortraits.FirstOrDefaultAsync(x => x.CompendiumId == id, ct);
        if (p is null) return;
        db.CompendiumPortraits.Remove(p);
        await db.SaveChangesAsync(ct);
        await scenes.RepublishTokensOfAsync(t => t.CompendiumNpcId == id, ct);
    }

    private static async Task<byte[]> ReadAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static NotFoundException NoImage() => new(L.T("Este NPC não tem imagem.", "This NPC has no image."));
}
