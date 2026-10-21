using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Npcs;

/// <summary>Rules, validation, generation and persistence of NPC sheets.</summary>
public sealed class NpcService(
    IAppDbContext db,
    NpcRuleBook rules,
    NpcValidator validator,
    NpcGenerator generator,
    NpcCatalogProvider catalogs,
    IValidator<NpcSaveRequest> saveValidator)
{
    internal static readonly JsonSerializerOptions SheetJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public NpcRulesDto GetRules() => NpcRulesMapper.Map(rules, validator);

    public async Task<NpcValidationReport> ValidateAsync(NpcSheet sheet, CancellationToken ct) =>
        validator.Validate(sheet, await catalogs.GetAsync(ct));

    public async Task<NpcGenerationResult> GenerateAsync(NpcGenerationRequest request, CancellationToken ct) =>
        generator.Generate(request, await catalogs.GetAsync(ct));

    /// <summary>
    /// Installs an implant in the manual editor using the same planner as the generator: missing foundational
    /// pieces are added, Option Slots and pairs are allocated and forbidden items are refused. Budgets are not
    /// enforced here; the validator reports them.
    /// </summary>
    public async Task<NpcSheet> InstallCyberwareAsync(NpcInstallCyberwareRequest request, CancellationToken ct)
    {
        var catalog = await catalogs.GetAsync(ct);
        var sheet = request.Sheet.Normalize();
        var item = catalog.Find(request.ItemId) ?? throw new NotFoundException($"Item {request.ItemId} não encontrado.");
        if (!item.IsCyberwareBudget) throw RequestValidationException.For("itemId", $"{item.Name} não é cyberware.");

        var planner = new CyberwarePlanner(new NpcMechanics(rules), catalog)
            .WithForbidden(sheet.CyberwareRules.Where(r => r.State == CyberwareRuleState.Forbidden).Select(r => r.ItemId));
        planner.AddExisting(sheet.Cyberware);
        var error = planner.TryInstall(item, null);
        if (error is not null) throw RequestValidationException.For("itemId", error);
        return sheet with { Cyberware = planner.Entries.ToList() };
    }

    // ------------------------------------------------------------ persistence

    public async Task<IReadOnlyList<NpcSummaryDto>> ListAsync(CancellationToken ct) =>
        await db.Npcs.AsNoTracking()
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NpcSummaryDto(n.Id, n.Name, n.Concept, n.Level, n.Hardened, n.ProfileId, n.Status, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(ct);

    public async Task<NpcDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var npc = await FindAsync(id, ct);
        var sheet = Deserialize(npc.SheetJson);
        return new NpcDetailDto(npc.Id, npc.Status, npc.CreatedAt, npc.UpdatedAt, sheet, await ValidateAsync(sheet, ct));
    }

    public async Task<NpcDetailDto> CreateAsync(NpcSaveRequest request, CancellationToken ct)
    {
        var (sheet, status) = await PrepareAsync(request, ct);
        var now = DateTime.UtcNow;
        var npc = new Npc { CreatedAt = now };
        Apply(npc, sheet, status, now);
        db.Npcs.Add(npc);
        await db.SaveChangesAsync(ct);
        return await GetAsync(npc.Id, ct);
    }

    public async Task<NpcDetailDto> UpdateAsync(int id, NpcSaveRequest request, CancellationToken ct)
    {
        var npc = await FindAsync(id, ct);
        var (sheet, status) = await PrepareAsync(request, ct);
        Apply(npc, sheet, status, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<NpcDetailDto> DuplicateAsync(int id, CancellationToken ct)
    {
        var source = await FindAsync(id, ct);
        var sheet = Deserialize(source.SheetJson);
        var copyName = $"{sheet.Name} (cópia)";
        sheet = sheet with { Name = copyName.Length > 80 ? copyName[..80] : copyName };
        var now = DateTime.UtcNow;
        var npc = new Npc { CreatedAt = now };
        // The copy keeps the original's status: same mechanics, recomputed on the next save.
        Apply(npc, sheet, source.Status, now);
        db.Npcs.Add(npc);
        await db.SaveChangesAsync(ct);
        return await GetAsync(npc.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        db.Npcs.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The status is never taken from the client: it comes from the shared validator. A sheet with blocking
    /// errors is only saved when the user explicitly asks for a draft.
    /// </summary>
    private async Task<(NpcSheet Sheet, NpcStatus Status)> PrepareAsync(NpcSaveRequest request, CancellationToken ct)
    {
        var shape = await saveValidator.ValidateAsync(request, ct);
        if (!shape.IsValid)
            throw new RequestValidationException(shape.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

        var sheet = request.Sheet.Normalize();
        var report = await ValidateAsync(sheet, ct);
        if (report.IsValid) return (sheet, request.SaveAsDraft ? NpcStatus.Draft : report.Status);
        if (request.SaveAsDraft) return (sheet, NpcStatus.Draft);

        var errors = report.Issues.Where(i => i.Severity == NpcIssueSeverity.Error && !i.Overridden).Select(i => i.Message).ToArray();
        throw new RequestValidationException(new Dictionary<string, string[]>
        {
            ["sheet"] = ["A ficha tem erros impeditivos e não pode ser salva como válida. Corrija, registre exceções ou salve como rascunho.", .. errors],
        });
    }

    private static void Apply(Npc npc, NpcSheet sheet, NpcStatus status, DateTime now)
    {
        npc.Name = sheet.Name;
        npc.Concept = sheet.Concept;
        npc.Level = sheet.Level;
        npc.Hardened = sheet.Hardened;
        npc.ProfileId = sheet.ProfileId;
        npc.Status = status;
        npc.SheetJson = JsonSerializer.Serialize(sheet, SheetJson);
        npc.UpdatedAt = now;
    }

    private static NpcSheet Deserialize(string json) =>
        (JsonSerializer.Deserialize<NpcSheet>(json, SheetJson) ?? new NpcSheet()).Normalize();

    private async Task<Npc> FindAsync(int id, CancellationToken ct) =>
        await db.Npcs.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException($"NPC {id} não encontrado.");
}

/// <summary>Turns the rule book into the documented view the editor shows (values plus sources).</summary>
internal static class NpcRulesMapper
{
    public static NpcRulesDto Map(NpcRuleBook rules, NpcValidator validator)
    {
        var d = rules.Document;
        var templates = rules.AllTemplates().Select(t =>
        {
            var baseRules = d.Templates.First(x => x.Level == t.Level);
            return new NpcTemplateDto(t.Level, t.Hardened, t.DisplayName, baseRules.Summary.Value, validator.Summary(t),
            [
                Fact("statPoints", "Pontos de STAT", t.StatPoints.Value.ToString(), t.StatPoints),
                Fact("maxStat", "STAT máximo", t.MaxStat.Value.ToString(), t.MaxStat),
                Fact("skillPoints", "Pontos de perícia", t.SkillPoints.Value.ToString(), t.SkillPoints),
                Fact("maxSkill", "Nível máximo de perícia", t.MaxSkill.Value.ToString(), t.MaxSkill),
                Fact("suggestedHp", "HP sugerido", t.SuggestedHp.Value.ToString(), t.SuggestedHp),
                Fact("suggestedSp", "SP sugerido", t.SuggestedSp.Value.ToString(), t.SuggestedSp),
                Fact("combatSkillBase", "Base das perícias de combate importantes", t.CombatSkillBase.Value.ToString(), t.CombatSkillBase),
                Fact("nonCombatSkillBase", "Base das perícias não combate importantes", t.NonCombatSkillBase.Value.ToString(), t.NonCombatSkillBase),
                Fact("gearBudget", "Orçamento de equipamento", NpcValidator.Eb(t.GearBudget.Value), t.GearBudget),
                Fact("cyberwareBudget", "Orçamento de cyberware", t.CyberwareBudget.Value is { } c ? NpcValidator.Eb(c) : "Ilimitado", t.CyberwareBudget),
                Fact("weaponQuality", "Qualidade das armas", t.WeaponQuality.Value switch
                {
                    WeaponQualityRule.PoorOnly => "Poor Quality",
                    WeaponQualityRule.NoExcellent => "Sem Excellent Quality",
                    _ => "Sem restrição",
                }, t.WeaponQuality),
                Fact("roleRank", "Role Ability", RoleText(rules, t.RoleRank.Value), t.RoleRank),
                .. t.HardenedRoleRank is { } h ? [Fact("hardenedRoleRank", "Role Ability (Hardened)", RoleText(rules, h.Value), h)] : Array.Empty<RuleFactDto>(),
            ]);
        }).ToList();

        var general = new List<RuleFactDto>
        {
            Fact("statMin", "STAT mínimo", d.Stats.Min.Value.ToString(), d.Stats.Min),
            Fact("empMin", "EMP mínimo", d.Stats.EmpMin.Value.ToString(), d.Stats.EmpMin),
            Fact("statAbsoluteMax", "STAT máximo absoluto", d.Stats.AbsoluteMax.Value.ToString(), d.Stats.AbsoluteMax),
            Fact("luck", "LUCK", d.Stats.Luck.Value, d.Stats.Luck),
            Fact("hp", "Hit Points", $"{d.Derived.HpBase.Value} + {d.Derived.HpPerAverage.Value} × média de BODY e WILL (para cima)", d.Derived.HpBase),
            Fact("seriouslyWounded", "Seriously Wounded", d.Derived.SeriouslyWounded.Value, d.Derived.SeriouslyWounded),
            Fact("deathSave", "Death Save", d.Derived.DeathSave.Value, d.Derived.DeathSave),
            Fact("linearFrame", "Implanted Linear Frame", d.Derived.ImplantedLinearFrame.Value, d.Derived.ImplantedLinearFrame),
            Fact("skillBase", "Skill Base", d.Derived.SkillBase.Value, d.Derived.SkillBase),
            Fact("armorPenalty", "Penalidade de armadura", d.Derived.ArmorPenalty.Value, d.Derived.ArmorPenalty),
            Fact("armorLayering", "SP por local", d.Derived.ArmorLayering.Value, d.Derived.ArmorLayering),
            Fact("doubleCost", "Perícias (x2)", $"{d.SkillCost.DoubleCost.Value} pontos por nível", d.SkillCost.DoubleCost),
            Fact("doubleCostBudget", "(x2) no orçamento de NPC", "Aplica o custo do Core", d.SkillCost.AppliesToNpcBudget),
            Fact("optionSlots", "Slots de opção sem dado no catálogo", $"{d.Cyberware.OptionSlotDefault.Value}", d.Cyberware.OptionSlotDefault),
            Fact("installCost", "Custo de instalação", "Incluído no preço", d.Cyberware.InstallCostIncluded),
            Fact("autoPick", "Pré-requisitos em texto livre", "Não escolhidos automaticamente", d.Cyberware.AutoPickExcludesFreeTextRequirements),
            Fact("armorPerLocation", "Uma armadura por local", "Corpo e cabeça comprados à parte", d.Armor.OneArmorPerLocation),
            Fact("empGuideline", "EMP e cyberware", $"-1 EMP a cada {d.EmpGuideline.PiecesPerPoint.Value} peças (opcional)", d.EmpGuideline.PiecesPerPoint),
            new("weaponQualityPricing", "Preço por qualidade de arma",
                string.Join(" · ", d.WeaponQualityPricing.Rows.Select(r => $"{r.Standard} → Poor {r.Poor} / Excellent {r.Excellent}")), d.WeaponQualityPricing.Source, false, null),
        };
        general.AddRange(d.Cyberware.Pools.Select(p => new RuleFactDto($"pool:{p.Key}", p.Label, $"{p.Slots} slots", p.Source, false, null)));
        general.AddRange(d.Cyberware.Capacity.Select(c => new RuleFactDto($"capacity:{c.Label}", c.Label, $"máx. {c.Max}", c.Source, c.Complementary, c.Note)));
        general.AddRange(d.Cyberware.ExclusiveGroups.Select(g => new RuleFactDto($"exclusive:{g.Label}", g.Label, $"máx. {g.Max} ({string.Join(", ", g.Slugs)})", g.Source, false, null)));
        general.AddRange(d.Cyberware.Overrides.Select(o => new RuleFactDto($"override:{o.Slug}", o.Slug,
            $"{(o.SlotsRequired is { } r ? $"ocupa {r} slot" : "")}{(o.SlotsProvided is { } p ? $", comporta {p}" : "")}", o.Source, false, o.Note)));
        general.AddRange(d.HardenedGuidance.Select((h, i) => Fact($"hardened:{i}", "Hardened", h.Value, h)));
        general.Add(Fact("maxAttempts", "Tentativas do gerador", d.Generator.MaxAttempts.Value.ToString(), d.Generator.MaxAttempts));
        general.Add(Fact("cyberSpend", "Gasto de cyberware do gerador",
            $"{d.Generator.CyberwareSpendRange.Value.Min:P0}–{d.Generator.CyberwareSpendRange.Value.Max:P0} do orçamento", d.Generator.CyberwareSpendRange));
        general.Add(Fact("unlimitedCyber", "Referência para orçamento ilimitado", NpcValidator.Eb(d.Generator.UnlimitedCyberwareReference.Value), d.Generator.UnlimitedCyberwareReference));
        general.AddRange(d.RolePresence.Select(kv => new RuleFactDto($"rolePresence:{kv.Key}", $"Chance de Role ({kv.Value.Label})",
            $"{kv.Value.Probability:P0}", null, kv.Value.Complementary, null)));
        general.Add(new RuleFactDto("profiles", "Perfis de inimigo", string.Join(", ", d.Profiles.Select(p => p.Label)), null, true,
            "Os perfis (pesos de STAT, perícias, armas e implantes preferidos) orientam o gerador e não são regras dos livros."));

        return new NpcRulesDto(
            d.Books, d.About, rules.StatKeys, templates,
            d.Skills.Select(s => new SkillDto(s.Id, s.Name, s.Stat, s.Category, s.Combat, s.DoubleCost, s.Specialized, s.Page, s.NamePt)).ToList(),
            d.FreeSkills.Entries.Select(e => new FreeSkillDto(e.Skill, e.Specialization, e.Level)).ToList(),
            d.FreeSkills.Source,
            d.Profiles.Select(p => new ProfileDto(p.Id, p.Label, p.Description, p.Role, p.KeyCombatSkills, p.KeyNonCombatSkills)).ToList(),
            d.Roles, d.Generator.Languages, d.Generator.Homes, general);
    }

    private static string RoleText(NpcRuleBook rules, RoleGuideline g) =>
        $"{rules.Document.RolePresence.GetValueOrDefault(g.Presence)?.Label ?? g.Presence.ToString()}: Rank {g.Rank}";

    private static RuleFactDto Fact<T>(string key, string label, string value, Ruled<T> r) =>
        new(key, label, value, r.Source, r.Complementary, r.Note);
}
