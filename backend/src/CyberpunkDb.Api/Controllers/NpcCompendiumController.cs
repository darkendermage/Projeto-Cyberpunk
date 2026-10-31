using CyberpunkDb.Application.Npcs.Compendium;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>Read-only compendium of the named NPCs of the books (stat blocks and profiles).</summary>
[ApiController]
[Route("api/npc-compendium")]
public sealed class NpcCompendiumController(NpcCompendium compendium) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<CompendiumNpcSummary> List() => compendium.ListSummaries();

    [HttpGet("{id}")]
    public CompendiumNpcDetail Get(string id) => compendium.GetDetail(id);
}
