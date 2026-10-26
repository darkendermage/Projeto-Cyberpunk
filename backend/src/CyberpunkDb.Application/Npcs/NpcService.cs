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
using CyberpunkDb.Application.Localization;

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
        var item = catalog.Find(request.ItemId) ?? throw new NotFoundException(L.T($"Item {request.ItemId} não encontrado.", $"Item {request.ItemId} not found."));
        if (!item.IsCyberwareBudget) throw RequestValidationException.For("itemId", L.T($"{item.Name} não é cyberware.", $"{item.Name} isn't cyberware."));

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
        var copyName = L.T($"{sheet.Name} (cópia)", $"{sheet.Name} (copy)");
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
            ["sheet"] = [L.T("A ficha tem erros impeditivos e não pode ser salva como válida. Corrija, registre exceções ou salve como rascunho.", "The sheet has blocking errors and can't be saved as valid. Fix them, record exceptions or save as a draft."), .. errors],
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
        await db.Npcs.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException(L.T($"NPC {id} não encontrado.", $"NPC {id} not found."));
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
                Fact("statPoints", L.T("Pontos de STAT", "STAT points"), t.StatPoints.Value.ToString(), t.StatPoints),
                Fact("maxStat", L.T("STAT máximo", "Maximum STAT"), t.MaxStat.Value.ToString(), t.MaxStat),
                Fact("skillPoints", L.T("Pontos de perícia", "Skill points"), t.SkillPoints.Value.ToString(), t.SkillPoints),
                Fact("maxSkill", L.T("Nível máximo de perícia", "Maximum skill level"), t.MaxSkill.Value.ToString(), t.MaxSkill),
                Fact("suggestedHp", "HP sugerido", t.SuggestedHp.Value.ToString(), t.SuggestedHp),
                Fact("suggestedSp", "SP sugerido", t.SuggestedSp.Value.ToString(), t.SuggestedSp),
                Fact("combatSkillBase", L.T("Base das perícias de combate importantes", "Base of important combat skills"), t.CombatSkillBase.Value.ToString(), t.CombatSkillBase),
                Fact("nonCombatSkillBase", L.T("Base das perícias não combate importantes", "Base of important non-combat skills"), t.NonCombatSkillBase.Value.ToString(), t.NonCombatSkillBase),
                Fact("gearBudget", L.T("Orçamento de equipamento", "Gear budget"), NpcValidator.Eb(t.GearBudget.Value), t.GearBudget),
                Fact("cyberwareBudget", L.T("Orçamento de cyberware", "Cyberware budget"), t.CyberwareBudget.Value is { } c ? NpcValidator.Eb(c) : L.T("Ilimitado", "Unlimited"), t.CyberwareBudget),
                Fact("weaponQuality", L.T("Qualidade das armas", "Weapon quality"), t.WeaponQuality.Value switch
                {
                    WeaponQualityRule.PoorOnly => "Poor Quality",
                    WeaponQualityRule.NoExcellent => L.T("Sem Excellent Quality", "No Excellent Quality"),
                    _ => L.T("Sem restrição", "No restriction"),
                }, t.WeaponQuality),
                Fact("roleRank", "Role Ability", RoleText(rules, t.RoleRank.Value), t.RoleRank),
                .. t.HardenedRoleRank is { } h ? [Fact("hardenedRoleRank", "Role Ability (Hardened)", RoleText(rules, h.Value), h)] : Array.Empty<RuleFactDto>(),
            ]);
        }).ToList();

        var general = new List<RuleFactDto>
        {
            Fact("statMin", L.T("STAT mínimo", "Minimum STAT"), d.Stats.Min.Value.ToString(), d.Stats.Min),
            Fact("empMin", L.T("EMP mínimo", "Minimum EMP"), d.Stats.EmpMin.Value.ToString(), d.Stats.EmpMin),
            Fact("statAbsoluteMax", L.T("STAT máximo absoluto", "Absolute maximum STAT"), d.Stats.AbsoluteMax.Value.ToString(), d.Stats.AbsoluteMax),
            Fact("luck", "LUCK", d.Stats.Luck.Value, d.Stats.Luck),
            Fact("hp", "Hit Points", L.T($"{d.Derived.HpBase.Value} + {d.Derived.HpPerAverage.Value} × média de BODY e WILL (para cima)", $"{d.Derived.HpBase.Value} + {d.Derived.HpPerAverage.Value} × average of BODY and WILL (rounded up)"), d.Derived.HpBase),
            Fact("seriouslyWounded", "Seriously Wounded", d.Derived.SeriouslyWounded.Value, d.Derived.SeriouslyWounded),
            Fact("deathSave", "Death Save", d.Derived.DeathSave.Value, d.Derived.DeathSave),
            Fact("linearFrame", "Implanted Linear Frame", d.Derived.ImplantedLinearFrame.Value, d.Derived.ImplantedLinearFrame),
            Fact("skillBase", "Skill Base", d.Derived.SkillBase.Value, d.Derived.SkillBase),
            Fact("armorPenalty", L.T("Penalidade de armadura", "Armor penalty"), d.Derived.ArmorPenalty.Value, d.Derived.ArmorPenalty),
            Fact("armorLayering", L.T("SP por local", "SP per location"), d.Derived.ArmorLayering.Value, d.Derived.ArmorLayering),
            Fact("doubleCost", L.T("Perícias (x2)", "(x2) skills"), L.T($"{d.SkillCost.DoubleCost.Value} pontos por nível", $"{d.SkillCost.DoubleCost.Value} points per level"), d.SkillCost.DoubleCost),
            Fact("doubleCostBudget", L.T("(x2) no orçamento de NPC", "(x2) in the NPC budget"), L.T("Aplica o custo do Core", "Applies the Core cost"), d.SkillCost.AppliesToNpcBudget),
            Fact("optionSlots", L.T("Slots de opção sem dado no catálogo", "Option slots with no catalog data"), $"{d.Cyberware.OptionSlotDefault.Value}", d.Cyberware.OptionSlotDefault),
            Fact("installCost", L.T("Custo de instalação", "Installation cost"), L.T("Incluído no preço", "Included in the price"), d.Cyberware.InstallCostIncluded),
            Fact("autoPick", L.T("Pré-requisitos em texto livre", "Free-text prerequisites"), L.T("Não escolhidos automaticamente", "Not picked automatically"), d.Cyberware.AutoPickExcludesFreeTextRequirements),
            Fact("armorPerLocation", L.T("Uma armadura por local", "One armor per location"), L.T("Corpo e cabeça comprados à parte", "Body and head bought separately"), d.Armor.OneArmorPerLocation),
            Fact("empGuideline", "EMP e cyberware", L.T($"-1 EMP a cada {d.EmpGuideline.PiecesPerPoint.Value} peças (opcional)", $"-1 EMP per {d.EmpGuideline.PiecesPerPoint.Value} pieces (optional)"), d.EmpGuideline.PiecesPerPoint),
            new("weaponQualityPricing", L.T("Preço por qualidade de arma", "Price by weapon quality"),
                string.Join(" · ", d.WeaponQualityPricing.Rows.Select(r => $"{r.Standard} → Poor {r.Poor} / Excellent {r.Excellent}")), d.WeaponQualityPricing.Source, false, null),
        };
        general.AddRange(d.Cyberware.Pools.Select(p => new RuleFactDto($"pool:{p.Key}", p.Label, $"{p.Slots} slots", p.Source, false, null)));
        general.AddRange(d.Cyberware.Capacity.Select(c => new RuleFactDto($"capacity:{c.Label}", c.Label, L.T($"máx. {c.Max}", $"max. {c.Max}"), c.Source, c.Complementary, c.Note)));
        general.AddRange(d.Cyberware.ExclusiveGroups.Select(g => new RuleFactDto($"exclusive:{g.Label}", g.Label, L.T($"máx. {g.Max} ({string.Join(", ", g.Slugs)})", $"max. {g.Max} ({string.Join(", ", g.Slugs)})"), g.Source, false, null)));
        general.AddRange(d.Cyberware.Overrides.Select(o => new RuleFactDto($"override:{o.Slug}", o.Slug,
            L.T($"{(o.SlotsRequired is { } r ? $"ocupa {r} slot" : "")}{(o.SlotsProvided is { } p ? $", comporta {p}" : "")}", $"{(o.SlotsRequired is { } r2 ? $"takes {r2} slot" : "")}{(o.SlotsProvided is { } p2 ? $", holds {p2}" : "")}"), o.Source, false, o.Note)));
        general.AddRange(d.HardenedGuidance.Select((h, i) => Fact($"hardened:{i}", "Hardened", h.Value, h)));
        general.Add(Fact("maxAttempts", L.T("Tentativas do gerador", "Generator attempts"), d.Generator.MaxAttempts.Value.ToString(), d.Generator.MaxAttempts));
        general.Add(Fact("cyberSpend", L.T("Gasto de cyberware do gerador", "Generator cyberware spending"),
            L.T($"{d.Generator.CyberwareSpendRange.Value.Min:P0}–{d.Generator.CyberwareSpendRange.Value.Max:P0} do orçamento", $"{d.Generator.CyberwareSpendRange.Value.Min:P0}–{d.Generator.CyberwareSpendRange.Value.Max:P0} of the budget"), d.Generator.CyberwareSpendRange));
        general.Add(Fact("unlimitedCyber", L.T("Referência para orçamento ilimitado", "Reference for an unlimited budget"), NpcValidator.Eb(d.Generator.UnlimitedCyberwareReference.Value), d.Generator.UnlimitedCyberwareReference));
        general.AddRange(d.RolePresence.Select(kv => new RuleFactDto($"rolePresence:{kv.Key}", L.T($"Chance de Role ({kv.Value.Label})", $"Role chance ({kv.Value.Label})"),
            $"{kv.Value.Probability:P0}", null, kv.Value.Complementary, null)));
        general.Add(new RuleFactDto("profiles", L.T("Perfis de inimigo", "Enemy profiles"), string.Join(", ", d.Profiles.Select(p => p.Label)), null, true,
            L.T("Os perfis (pesos de STAT, perícias, armas e implantes preferidos) orientam o gerador e não são regras dos livros.", "The profiles (STAT weights, preferred skills, weapons and implants) guide the generator and aren't book rules.")));

        return new NpcRulesDto(
            d.Books, d.About, rules.StatKeys, templates,
            d.Skills.Select(s => new SkillDto(s.Id, s.Name, s.Stat, s.Category, s.Combat, s.DoubleCost, s.Specialized, s.Page, s.NamePt)).ToList(),
            d.FreeSkills.Entries.Select(e => new FreeSkillDto(e.Skill, e.Specialization, e.Level)).ToList(),
            d.FreeSkills.Source,
            d.Profiles.Select(p => new ProfileDto(p.Id, p.Label, p.Description, p.Role, p.KeyCombatSkills, p.KeyNonCombatSkills)).ToList(),
            d.Gangs.Select(MapGang).ToList(),
            d.Roles, d.Generator.Languages, d.Generator.Homes, general);
    }

    private static GangDto MapGang(NpcGang g)
    {
        var bias = g.Bias.Value ?? new GangBias();
        var facts = new List<RuleFactDto>
        {
            Fact("description", L.T("Quem são", "Who they are"), g.Description.Value, g.Description),
            Fact("aesthetic", L.T("Estética", "Look"), g.Aesthetic.Value, g.Aesthetic),
        };
        if (g.Territory.Value is { Length: > 0 } turf) facts.Add(Fact("territory", L.T("Território", "Turf"), string.Join(", ", turf), g.Territory));
        if (g.Languages.Value is { Count: > 0 } langs) facts.Add(Fact("languages", L.T("Idiomas nativos", "Native languages"), string.Join(", ", langs.Keys), g.Languages));
        facts.AddRange(g.Loadout.Select((l, i) => Fact($"loadout:{i}", L.T("Nos livros", "In the books"), l.Value, l)));
        facts.Add(Fact("bias", L.T("Preferências do gerador", "Generator preferences"), BiasText(bias), g.Bias));
        return new GangDto(g.Id, g.Label, g.Era, g.Kind, g.Description.Value, g.Aesthetic.Value,
            g.Territory.Value ?? [], (g.Languages.Value ?? []).Keys.ToList(),
            bias.Profiles.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList(),
            facts);
    }

    private static string BiasText(GangBias b)
    {
        var parts = new List<string>();
        if (b.WeaponClasses.Length > 0) parts.Add($"armas: {string.Join(", ", b.WeaponClasses.Select(c => EnumLabels.Label(c)))}");
        if (b.ArmorStyle is { } style) parts.Add(style switch { ArmorStyle.Heavy => L.T("armadura pesada", "heavy armor"), ArmorStyle.Medium => L.T("armadura média", "medium armor"), _ => L.T("armadura leve", "light armor") });
        if (b.AllCyberwareForbidden) parts.Add(L.T("nenhum cyberware", "no cyberware"));
        else if (b.CyberwareSpendRange is { } r) parts.Add(L.T($"gasto de cyberware {r.Min:P0}–{r.Max:P0} do orçamento", $"cyberware spending {r.Min:P0}–{r.Max:P0} of the budget"));
        if (b.ForbiddenCyberware.Length > 0) parts.Add($"evita {string.Join(", ", b.ForbiddenCyberware)}");
        if (b.Cyberware.Count > 0) parts.Add($"implantes: {string.Join(", ", b.Cyberware.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key))}");
        if (b.Gear.Count > 0) parts.Add($"itens: {string.Join(", ", b.Gear.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key))}");
        return string.Join(" · ", parts);
    }

    private static string RoleText(NpcRuleBook rules, RoleGuideline g) =>
        $"{rules.Document.RolePresence.GetValueOrDefault(g.Presence)?.Label ?? g.Presence.ToString()}: Rank {g.Rank}";

    private static RuleFactDto Fact<T>(string key, string label, string value, Ruled<T> r) =>
        new(key, label, value, r.Source, r.Complementary, r.Note);
}
