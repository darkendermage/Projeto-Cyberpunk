using CyberpunkDb.Application.Encounters;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// The combat table (Friday Night Firefight, CP:R p.168-188): encounters, the Initiative Queue, attacks, damage, Death
/// Saves and cover. Every change is a command validated on the server and answered with the updated encounter.
/// </summary>
[ApiController]
[Route("api/encounters")]
public sealed class EncountersController(EncounterService encounters) : ControllerBase
{
    [HttpGet("rules")]
    public EncounterRulesDto GetRules() => encounters.GetRules();

    [HttpGet]
    public Task<IReadOnlyList<EncounterSummaryDto>> List(CancellationToken ct) => encounters.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<EncounterDetailDto> Get(int id, CancellationToken ct) => encounters.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<EncounterDetailDto>> Create(CreateEncounterRequest request, CancellationToken ct)
    {
        var created = await encounters.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<EncounterDetailDto> Update(int id, UpdateEncounterRequest request, CancellationToken ct) => encounters.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await encounters.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/end")]
    public Task<EncounterDetailDto> End(int id, EncounterVersionRequest request, CancellationToken ct) => encounters.EndAsync(id, request, ct);

    [HttpPost("{id:int}/combatants")]
    public Task<EncounterDetailDto> AddCombatants(int id, AddCombatantsRequest request, CancellationToken ct) =>
        encounters.AddCombatantsAsync(id, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/remove")]
    public Task<EncounterDetailDto> RemoveCombatant(int id, string combatantId, EncounterVersionRequest request, CancellationToken ct) =>
        encounters.RemoveCombatantAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/initiative")]
    public Task<EncounterDetailDto> Initiative(int id, InitiativeRequest request, CancellationToken ct) => encounters.RollInitiativeAsync(id, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/initiative")]
    public Task<EncounterDetailDto> SetInitiative(int id, string combatantId, SetInitiativeRequest request, CancellationToken ct) =>
        encounters.SetInitiativeAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/active")]
    public Task<EncounterDetailDto> SetActive(int id, string combatantId, EncounterVersionRequest request, CancellationToken ct) =>
        encounters.SetActiveAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/next-turn")]
    public Task<EncounterDetailDto> NextTurn(int id, EncounterVersionRequest request, CancellationToken ct) => encounters.NextTurnAsync(id, request, ct);

    [HttpPost("{id:int}/attack")]
    public Task<EncounterDetailDto> Attack(int id, AttackRequest request, CancellationToken ct) => encounters.AttackAsync(id, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/damage")]
    public Task<EncounterDetailDto> Damage(int id, string combatantId, ManualDamageRequest request, CancellationToken ct) =>
        encounters.DamageCombatantAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/environment")]
    public Task<EncounterDetailDto> Environment(int id, string combatantId, EnvironmentDamageRequest request, CancellationToken ct) =>
        encounters.EnvironmentAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/heal")]
    public Task<EncounterDetailDto> Heal(int id, string combatantId, HealCombatantRequest request, CancellationToken ct) =>
        encounters.HealAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/stabilize")]
    public Task<EncounterDetailDto> Stabilize(int id, string combatantId, EncounterVersionRequest request, CancellationToken ct) =>
        encounters.StabilizeAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/death-save")]
    public Task<EncounterDetailDto> DeathSave(int id, string combatantId, DeathSaveCombatantRequest request, CancellationToken ct) =>
        encounters.DeathSaveAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/condition")]
    public Task<EncounterDetailDto> Condition(int id, string combatantId, ConditionRequest request, CancellationToken ct) =>
        encounters.ConditionAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/combatants/{combatantId}/cover")]
    public Task<EncounterDetailDto> TakeCover(int id, string combatantId, TakeCoverRequest request, CancellationToken ct) =>
        encounters.TakeCoverAsync(id, combatantId, request, ct);

    [HttpPost("{id:int}/covers")]
    public Task<EncounterDetailDto> AddCover(int id, CoverRequest request, CancellationToken ct) => encounters.AddCoverAsync(id, request, ct);

    [HttpPost("{id:int}/covers/{coverId}/damage")]
    public Task<EncounterDetailDto> DamageCover(int id, string coverId, CoverDamageRequest request, CancellationToken ct) =>
        encounters.DamageCoverAsync(id, coverId, request, ct);

    [HttpPost("{id:int}/covers/{coverId}/remove")]
    public Task<EncounterDetailDto> RemoveCover(int id, string coverId, EncounterVersionRequest request, CancellationToken ct) =>
        encounters.RemoveCoverAsync(id, coverId, request, ct);
}
