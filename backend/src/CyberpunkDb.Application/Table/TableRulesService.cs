using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table;

public sealed record RuleNoteDto(string Title, string Text, string Source);

// range DVs live in the frontend
public sealed record TableRulesDto(
    IReadOnlyList<CriticalInjuryDto> CriticalInjuries, IReadOnlyList<CoverMaterial> CoverMaterials, string CoverMaterialsSource,
    IReadOnlyList<CoverExample> CoverExamples, string CoverExamplesSource, IReadOnlyList<RuleNoteDto> Notes);

// same for GM and players, nothing private here
public sealed class TableRulesService(CharacterService characters, CombatRuleBook combat)
{
    public async Task<TableRulesDto> GetAsync(CancellationToken ct)
    {
        var d = combat.Document;
        var shell = d.ShotgunShell.Value;
        var suppressive = d.Suppressive.Value;
        var notes = new List<RuleNoteDto>
        {
            new(L.T("Cobertura", "Cover"), d.CoverRules.Value, d.CoverRules.Source),
            new(L.T("Explosivos", "Explosives"), d.Explosives.Value, d.Explosives.Source),
            new(L.T("Cartucho de escopeta", "Shotgun shell"),
                L.T($"DV{shell.Dv} contra todos à frente até {shell.RangeMeters} m; {shell.Damage}, rolado uma vez.", $"DV{shell.Dv} against everyone in front up to {shell.RangeMeters} m; {shell.Damage}, rolled once."), d.ShotgunShell.Source),
            new(L.T("Fogo de supressão", "Suppressive Fire"),
                L.T($"{suppressive.Check}, até {suppressive.RangeMeters} m. {suppressive.Effect}", $"{suppressive.Check}, up to {suppressive.RangeMeters} m. {suppressive.Effect}"), d.Suppressive.Source),
        };
        var injuries = (await characters.GetRulesAsync(ct)).CriticalInjuries;
        return new TableRulesDto(injuries, d.CoverMaterials.Value, d.CoverMaterials.Source, d.CoverExamples.Value, d.CoverExamples.Source, notes);
    }
}
