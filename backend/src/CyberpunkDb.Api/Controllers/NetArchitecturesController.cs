using System.Text;
using CyberpunkDb.Application.NetArchitectures;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// NET Architectures (CP:R p.210-218, Going Quiet): generation, the GM's edits floor by floor, Demons, Watchers and the
/// export. Every change is a command validated on the server and answered with the updated Architecture.
/// </summary>
[ApiController]
[Route("api/net-architectures")]
public sealed class NetArchitecturesController(NetArchitectureService architectures) : ControllerBase
{
    [HttpGet("rules")]
    public Task<NetArchitectureRulesDto> GetRules(CancellationToken ct) => architectures.GetRulesAsync(ct);

    [HttpGet]
    public Task<IReadOnlyList<NetArchitectureSummaryDto>> List(CancellationToken ct) => architectures.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<NetArchitectureDetailDto> Get(int id, CancellationToken ct) => architectures.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<NetArchitectureDetailDto>> Create(GenerateArchitectureRequest request, CancellationToken ct)
    {
        var created = await architectures.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<NetArchitectureDetailDto> Update(int id, UpdateArchitectureRequest request, CancellationToken ct) =>
        architectures.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await architectures.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/regenerate")]
    public Task<NetArchitectureDetailDto> Regenerate(int id, RegenerateArchitectureRequest request, CancellationToken ct) =>
        architectures.RegenerateAsync(id, request, ct);

    [HttpPost("{id:int}/floors/{floorId}/reroll")]
    public Task<NetArchitectureDetailDto> RerollFloor(int id, string floorId, VersionRequest request, CancellationToken ct) =>
        architectures.RerollFloorAsync(id, floorId, request, ct);

    [HttpPost("{id:int}/floors/{floorId}/set")]
    public Task<NetArchitectureDetailDto> SetFloor(int id, string floorId, SetFloorRequest request, CancellationToken ct) =>
        architectures.SetFloorAsync(id, floorId, request, ct);

    [HttpPost("{id:int}/floors/{floorId}/lock")]
    public Task<NetArchitectureDetailDto> LockFloor(int id, string floorId, LockFloorRequest request, CancellationToken ct) =>
        architectures.LockFloorAsync(id, floorId, request, ct);

    [HttpPost("{id:int}/contents/{contentId}/ice")]
    public Task<NetArchitectureDetailDto> SwapIce(int id, string contentId, SwapIceRequest request, CancellationToken ct) =>
        architectures.SwapIceAsync(id, contentId, request, ct);

    [HttpPost("{id:int}/contents/{contentId}/node")]
    public Task<NetArchitectureDetailDto> SetNode(int id, string contentId, SetNodeRequest request, CancellationToken ct) =>
        architectures.SetNodeAsync(id, contentId, request, ct);

    [HttpPost("{id:int}/demons")]
    public Task<NetArchitectureDetailDto> AddDemon(int id, DemonRequest request, CancellationToken ct) =>
        architectures.AddDemonAsync(id, request, ct);

    [HttpPut("{id:int}/demons/{demonId}")]
    public Task<NetArchitectureDetailDto> UpdateDemon(int id, string demonId, DemonRequest request, CancellationToken ct) =>
        architectures.UpdateDemonAsync(id, demonId, request, ct);

    [HttpPost("{id:int}/demons/{demonId}/remove")]
    public Task<NetArchitectureDetailDto> RemoveDemon(int id, string demonId, VersionRequest request, CancellationToken ct) =>
        architectures.RemoveDemonAsync(id, demonId, request, ct);

    [HttpPost("{id:int}/netrunners")]
    public Task<NetArchitectureDetailDto> AddNetrunner(int id, NetrunnerRequest request, CancellationToken ct) =>
        architectures.AddNetrunnerAsync(id, request, ct);

    [HttpPut("{id:int}/netrunners/{netrunnerId}")]
    public Task<NetArchitectureDetailDto> UpdateNetrunner(int id, string netrunnerId, NetrunnerRequest request, CancellationToken ct) =>
        architectures.UpdateNetrunnerAsync(id, netrunnerId, request, ct);

    [HttpPost("{id:int}/netrunners/{netrunnerId}/remove")]
    public Task<NetArchitectureDetailDto> RemoveNetrunner(int id, string netrunnerId, VersionRequest request, CancellationToken ct) =>
        architectures.RemoveNetrunnerAsync(id, netrunnerId, request, ct);

    /// <summary>The floor tables as text (<c>format=text</c>) or Markdown (<c>format=markdown</c>), ready to paste.</summary>
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, [FromQuery] string format = "text", CancellationToken ct = default)
    {
        var markdown = string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "md", StringComparison.OrdinalIgnoreCase);
        var (fileName, content) = await architectures.ExportAsync(id, markdown, ct);
        return File(Encoding.UTF8.GetBytes(content), markdown ? "text/markdown; charset=utf-8" : "text/plain; charset=utf-8", fileName);
    }
}
