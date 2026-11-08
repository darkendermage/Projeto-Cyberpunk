using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Compendium;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

// read-only, plus the GM's images of them
[ApiController]
[Route("api/npc-compendium")]
public sealed class NpcCompendiumController(NpcCompendium compendium, NpcPortraitService portraits) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<CompendiumNpcSummary> List() => compendium.ListSummaries();

    [HttpGet("portraits")]
    public Task<IReadOnlyDictionary<string, DateTime>> Portraits(CancellationToken ct) => portraits.CompendiumStampsAsync(ct);

    [HttpGet("{id}")]
    public CompendiumNpcDetail Get(string id) => compendium.GetDetail(id);

    [HttpGet("{id}/portrait")]
    public async Task<IActionResult> Portrait(string id, CancellationToken ct)
    {
        var (data, type, updated) = await portraits.GetCompendiumAsync(id, ct);
        Response.Headers.CacheControl = "no-cache";
        return File(data, type, lastModified: updated, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{updated.Ticks}\""));
    }

    [HttpPut("{id}/portrait")]
    [RequestSizeLimit(CharacterService.PortraitMaxBytes + 64 * 1024)]
    public async Task<IActionResult> SetPortrait(string id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        await portraits.SetCompendiumAsync(id, file.ContentType, stream, file.Length, ct);
        return NoContent();
    }

    [HttpDelete("{id}/portrait")]
    public async Task<IActionResult> DeletePortrait(string id, CancellationToken ct)
    {
        await portraits.DeleteCompendiumAsync(id, ct);
        return NoContent();
    }
}
