using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Validation;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

[ApiController]
[Route("api/npcs")]
public sealed class NpcsController(NpcService npcs) : ControllerBase
{
    [HttpGet("rules")]
    public NpcRulesDto GetRules() => npcs.GetRules();

    [HttpPost("validate")]
    public Task<NpcValidationReport> Validate(NpcSheet sheet, CancellationToken ct) => npcs.ValidateAsync(sheet, ct);

    [HttpPost("generate")]
    public Task<NpcGenerationResult> Generate(NpcGenerationRequest request, CancellationToken ct) => npcs.GenerateAsync(request, ct);

    [HttpPost("cyberware/install")]
    public Task<NpcSheet> InstallCyberware(NpcInstallCyberwareRequest request, CancellationToken ct) => npcs.InstallCyberwareAsync(request, ct);

    [HttpGet]
    public Task<IReadOnlyList<NpcSummaryDto>> List(CancellationToken ct) => npcs.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<NpcDetailDto> Get(int id, CancellationToken ct) => npcs.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<NpcDetailDto>> Create(NpcSaveRequest request, CancellationToken ct)
    {
        var npc = await npcs.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = npc.Id }, npc);
    }

    [HttpPut("{id:int}")]
    public Task<NpcDetailDto> Update(int id, NpcSaveRequest request, CancellationToken ct) => npcs.UpdateAsync(id, request, ct);

    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<NpcDetailDto>> Duplicate(int id, CancellationToken ct)
    {
        var npc = await npcs.DuplicateAsync(id, ct);
        return CreatedAtAction(nameof(Get), new { id = npc.Id }, npc);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await npcs.DeleteAsync(id, ct);
        return NoContent();
    }
}
