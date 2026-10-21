using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// Player characters. Reads return the sheet with every derived value and the validation report; every change is a
/// command validated on the server and answered with the updated character.
/// </summary>
[ApiController]
[Route("api/characters")]
public sealed class CharactersController(CharacterService characters) : ControllerBase
{
    [HttpGet("rules")]
    public Task<CharacterRulesDto> GetRules(CancellationToken ct) => characters.GetRulesAsync(ct);

    [HttpGet]
    public Task<IReadOnlyList<CharacterSummaryDto>> List(CancellationToken ct) => characters.ListAsync(ct);

    [HttpGet("store")]
    public Task<StorePageDto> Store([FromQuery] string? q, [FromQuery] ItemCategory? category, [FromQuery] int page, [FromQuery] int pageSize,
        CancellationToken ct) =>
        characters.StoreAsync(q, category, page == 0 ? 1 : page, pageSize == 0 ? 24 : pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<CharacterDetailDto> Get(int id, CancellationToken ct) => characters.GetAsync(id, ct);

    [HttpGet("{id:int}/events")]
    public Task<IReadOnlyList<CharacterEventDto>> Events(int id, [FromQuery] CharacterEventKind? kind, CancellationToken ct) =>
        characters.EventsAsync(id, kind, ct);

    [HttpPost]
    public async Task<ActionResult<CharacterDetailDto>> Create(CreateCharacterRequest request, CancellationToken ct)
    {
        var created = await characters.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<CharacterDetailDto>> Duplicate(int id, CancellationToken ct)
    {
        var copy = await characters.DuplicateAsync(id, ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, copy);
    }

    /// <summary>Downloads one character as a file (sheet, history and portrait) to import elsewhere.</summary>
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var (content, fileName) = await characters.ExportAsync(id, ct);
        return File(content, "application/json", fileName);
    }

    /// <summary>Creates a new character from an exported file; nothing existing is overwritten.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(CharacterService.ImportMaxBytes)]
    public async Task<ActionResult<CharacterDetailDto>> Import(CharacterExportFile file, CancellationToken ct)
    {
        var created = await characters.ImportAsync(file, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await characters.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public Task<CharacterDetailDto> Save(int id, SaveSheetRequest request, CancellationToken ct) => characters.SaveSheetAsync(id, request, ct);

    // ------------------------------------------------------------ creation

    [HttpPost("{id:int}/creation/reset")]
    public Task<CharacterDetailDto> Reset(int id, ResetCreationRequest r, CancellationToken ct) => characters.ResetCreationAsync(id, r, ct);

    [HttpPost("{id:int}/creation/template-row")]
    public Task<CharacterDetailDto> TemplateRow(int id, TemplateRowRequest r, CancellationToken ct) => characters.TemplateRowAsync(id, r, ct);

    [HttpPost("{id:int}/creation/stat-rolls")]
    public Task<CharacterDetailDto> StatRolls(int id, StatRollsRequest r, CancellationToken ct) => characters.StatRollsAsync(id, r, ct);

    [HttpPost("{id:int}/creation/skill-choice")]
    public Task<CharacterDetailDto> SkillChoice(int id, SkillChoiceRequest r, CancellationToken ct) => characters.SkillChoiceAsync(id, r, ct);

    [HttpPost("{id:int}/creation/finalize")]
    public Task<CharacterDetailDto> Finalize(int id, VersionRequest r, CancellationToken ct) => characters.FinalizeAsync(id, r, ct);

    // ------------------------------------------------------------ inventory and money

    [HttpPost("{id:int}/purchase")]
    public Task<CharacterDetailDto> Purchase(int id, PurchaseRequest r, CancellationToken ct) => characters.PurchaseAsync(id, r, ct);

    [HttpPost("{id:int}/purchase/undo")]
    public Task<CharacterDetailDto> UndoPurchase(int id, InstanceRequest r, CancellationToken ct) => characters.UndoPurchaseAsync(id, r, ct);

    [HttpPost("{id:int}/equip")]
    public Task<CharacterDetailDto> Equip(int id, EquipRequest r, CancellationToken ct) => characters.EquipAsync(id, r, ct);

    [HttpPost("{id:int}/weapons/fire")]
    public Task<CharacterDetailDto> Fire(int id, FireRequest r, CancellationToken ct) => characters.FireAsync(id, r, ct);

    [HttpPost("{id:int}/weapons/reload")]
    public Task<CharacterDetailDto> Reload(int id, ReloadRequest r, CancellationToken ct) => characters.ReloadAsync(id, r, ct);

    [HttpPost("{id:int}/balance")]
    public Task<CharacterDetailDto> AdjustBalance(int id, AdjustBalanceRequest r, CancellationToken ct) => characters.AdjustBalanceAsync(id, r, ct);

    // ------------------------------------------------------------ cyberware

    [HttpPost("{id:int}/cyberware/preview")]
    public Task<InstallPreviewDto> PreviewInstall(int id, InstallCyberwareRequest r, CancellationToken ct) => characters.PreviewInstallAsync(id, r, ct);

    [HttpPost("{id:int}/cyberware/install")]
    public Task<CharacterDetailDto> InstallCyberware(int id, InstallCyberwareRequest r, CancellationToken ct) => characters.InstallCyberwareAsync(id, r, ct);

    [HttpPost("{id:int}/cyberware/remove")]
    public Task<CharacterDetailDto> RemoveCyberware(int id, RemoveCyberwareRequest r, CancellationToken ct) => characters.RemoveCyberwareAsync(id, r, ct);

    [HttpPost("{id:int}/cyberware/replace")]
    public Task<CharacterDetailDto> ReplaceCyberware(int id, ReplaceCyberwareRequest r, CancellationToken ct) => characters.ReplaceCyberwareAsync(id, r, ct);

    [HttpPost("{id:int}/therapy")]
    public Task<CharacterDetailDto> Therapy(int id, TherapyRequest r, CancellationToken ct) => characters.TherapyAsync(id, r, ct);

    [HttpPost("{id:int}/weapons/attach")]
    public Task<CharacterDetailDto> Attach(int id, AttachRequest r, CancellationToken ct) => characters.AttachAsync(id, r, ct);

    [HttpPost("{id:int}/weapons/detach")]
    public Task<CharacterDetailDto> Detach(int id, AttachRequest r, CancellationToken ct) => characters.DetachAsync(id, r, ct);

    // ------------------------------------------------------------ progression

    [HttpPost("{id:int}/progression/ip")]
    public Task<CharacterDetailDto> AwardIp(int id, AwardIpRequest r, CancellationToken ct) => characters.AwardIpAsync(id, r, ct);

    [HttpPost("{id:int}/progression/skill")]
    public Task<CharacterDetailDto> ImproveSkill(int id, ImproveSkillRequest r, CancellationToken ct) => characters.ImproveSkillAsync(id, r, ct);

    [HttpPost("{id:int}/progression/role")]
    public Task<CharacterDetailDto> ImproveRole(int id, ImproveRoleRequest r, CancellationToken ct) => characters.ImproveRoleAsync(id, r, ct);

    [HttpPost("{id:int}/progression/multiclass")]
    public Task<CharacterDetailDto> AddRole(int id, AddRoleRequest r, CancellationToken ct) => characters.AddRoleAsync(id, r, ct);

    [HttpPost("{id:int}/housing/pay")]
    public Task<CharacterDetailDto> PayMonths(int id, PayMonthsRequest r, CancellationToken ct) => characters.PayMonthsAsync(id, r, ct);

    [HttpPost("{id:int}/inventory/remove")]
    public Task<CharacterDetailDto> RemoveItem(int id, RemoveItemRequest r, CancellationToken ct) => characters.RemoveItemAsync(id, r, ct);

    [HttpPost("{id:int}/luck")]
    public Task<CharacterDetailDto> Luck(int id, LuckRequest r, CancellationToken ct) => characters.LuckAsync(id, r, ct);

    // ------------------------------------------------------------ combat

    [HttpPost("{id:int}/combat/damage")]
    public Task<CharacterDetailDto> Damage(int id, DamageRequest r, CancellationToken ct) => characters.DamageAsync(id, r, ct);

    [HttpPost("{id:int}/combat/heal")]
    public Task<CharacterDetailDto> Heal(int id, HealRequest r, CancellationToken ct) => characters.HealAsync(id, r, ct);

    [HttpPost("{id:int}/combat/stabilize")]
    public Task<CharacterDetailDto> Stabilize(int id, VersionRequest r, CancellationToken ct) => characters.StabilizeAsync(id, r, ct);

    [HttpPost("{id:int}/combat/death-save")]
    public Task<CharacterDetailDto> DeathSave(int id, DeathSaveRequest r, CancellationToken ct) => characters.DeathSaveAsync(id, r, ct);

    [HttpPost("{id:int}/combat/conditions/clear")]
    public Task<CharacterDetailDto> ClearConditions(int id, VersionRequest r, CancellationToken ct) => characters.ClearConditionsAsync(id, r, ct);

    [HttpPost("{id:int}/armor/ablate")]
    public Task<CharacterDetailDto> Ablate(int id, AblateRequest r, CancellationToken ct) => characters.AblateAsync(id, r, ct);

    [HttpPost("{id:int}/armor/repair")]
    public Task<CharacterDetailDto> Repair(int id, RepairArmorRequest r, CancellationToken ct) => characters.RepairArmorAsync(id, r, ct);

    [HttpPost("{id:int}/injuries")]
    public Task<CharacterDetailDto> AddInjury(int id, AddInjuryRequest r, CancellationToken ct) => characters.AddInjuryAsync(id, r, ct);

    [HttpPost("{id:int}/injuries/{injuryId}")]
    public Task<CharacterDetailDto> UpdateInjury(int id, string injuryId, UpdateInjuryRequest r, CancellationToken ct) =>
        characters.UpdateInjuryAsync(id, injuryId, r, ct);

    [HttpPost("{id:int}/injuries/{injuryId}/remove")]
    public Task<CharacterDetailDto> RemoveInjury(int id, string injuryId, VersionRequest r, CancellationToken ct) =>
        characters.RemoveInjuryAsync(id, injuryId, r, ct);

    // ------------------------------------------------------------ portrait

    [HttpGet("{id:int}/portrait")]
    public async Task<IActionResult> Portrait(int id, CancellationToken ct)
    {
        var (data, type, updated) = await characters.GetPortraitAsync(id, ct);
        Response.Headers.CacheControl = "no-cache";
        return File(data, type, lastModified: updated, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{updated.Ticks}\""));
    }

    [HttpPut("{id:int}/portrait")]
    [RequestSizeLimit(CharacterService.PortraitMaxBytes + 64 * 1024)]
    public async Task<IActionResult> SetPortrait(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        await characters.SetPortraitAsync(id, file.ContentType, stream, file.Length, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/portrait")]
    public async Task<IActionResult> DeletePortrait(int id, CancellationToken ct)
    {
        await characters.DeletePortraitAsync(id, ct);
        return NoContent();
    }
}
