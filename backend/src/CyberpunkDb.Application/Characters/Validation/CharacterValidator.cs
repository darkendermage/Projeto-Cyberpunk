using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Validation;

/// <summary>
/// Single validator for player characters. While the character is being created it enforces the rules of its
/// creation method (CP:R p.73–117); afterwards only structural rules apply, because Improvement Points legitimately
/// take skills past the creation limits. The status is always recalculated here, never trusted from the client.
/// </summary>
public sealed class CharacterValidator(CharacterRuleBook rules, Roles.RoleModuleRegistry? roleModules = null)
{
    private const int TechnicalStatMax = 20;

    public CharacterValidationReport Validate(CharacterSheet sheet, NpcCatalog catalog)
    {
        sheet = sheet.Normalize();
        var c = new Context(sheet, catalog, CreationLimits.For(rules, sheet.Creation));

        ValidateIdentity(c);
        ValidateRoles(c);
        ValidateStatsShape(c);
        ValidateSkillsShape(c);
        ValidateWallet(c);
        if (roleModules is not null) c.Issues.AddRange(roleModules.Validate(sheet));

        CreationBudget? stats = null, skills = null, gear = null, fashion = null;
        if (!sheet.Creation.Finalized)
        {
            switch (sheet.Creation.Method)
            {
                case CreationMethod.Streetrat:
                    ValidateStreetratStats(c);
                    ValidateTemplateSkills(c);
                    ValidateStartingChoices(c);
                    break;
                case CreationMethod.Edgerunner:
                    ValidateEdgerunnerStats(c);
                    skills = ValidatePointBuySkills(c);
                    ValidateStartingChoices(c);
                    break;
                default:
                    stats = ValidatePointBuyStats(c);
                    skills = ValidatePointBuySkills(c);
                    (gear, fashion) = ValidateCreationBudgets(c);
                    break;
            }
        }

        var errors = c.Issues.Any(i => i.Severity == CharacterIssueSeverity.Error);
        var status = !sheet.Creation.Finalized ? CharacterStatus.Creating
            : errors ? CharacterStatus.NeedsAttention : CharacterStatus.Complete;
        return new CharacterValidationReport(status, !sheet.Creation.Finalized && !errors, stats, skills, gear, fashion, c.Issues);
    }

    // ------------------------------------------------------------ identity and roles

    private void ValidateIdentity(Context c)
    {
        if (string.IsNullOrWhiteSpace(c.Sheet.Identity.Name))
            c.Error("NAME_REQUIRED", L.T("O personagem precisa de um nome.", "The character needs a name."), null, null, "identity.name");
        if (c.Sheet.Identity.NativeLanguage is null)
            c.Error("NATIVE_LANGUAGE_REQUIRED", L.T("Escolha o idioma de origem cultural.", "Pick the cultural origin language."),
                L.T("Ele entra como Language no nível 4, sem custo.", "It comes in as Language at level 4, free."), rules.Document.Creation.OriginLanguageLevel.Reference, "identity.nativeLanguage");
        else if (CreationCosts.NativeLanguageKey(c.Sheet) is not { } key)
            c.Error("NATIVE_LANGUAGE_STREETSLANG", L.T("Streetslang já é perícia básica de todos; o idioma de origem é outro.", "Streetslang is already everyone's basic skill; the native language is another one."),
                L.T("Escolha a língua da sua região cultural.", "Pick the language of your cultural region."), rules.Document.Creation.OriginLanguageLevel.Reference, "identity.nativeLanguage");
        else
        {
            var min = rules.Document.Creation.OriginLanguageLevel.Value;
            if (c.Sheet.Skills.GetValueOrDefault(key) < min)
                c.Error("NATIVE_LANGUAGE_LEVEL", L.T($"Language ({c.Sheet.Identity.NativeLanguage}) deve estar no nível {min} ou mais.", $"Language ({c.Sheet.Identity.NativeLanguage}) must be at level {min} or more."),
                    null, rules.Document.Creation.OriginLanguageLevel.Reference, $"skills.{key}");
            var region = rules.Document.Lifepath.CulturalOrigins.FirstOrDefault(o =>
                o.Region.Equals(c.Sheet.Lifepath.CulturalRegion, StringComparison.OrdinalIgnoreCase));
            if (region is not null && !region.Languages.Contains(c.Sheet.Identity.NativeLanguage, StringComparer.OrdinalIgnoreCase))
                c.Info("NATIVE_LANGUAGE_OUTSIDE_REGION", L.T($"{c.Sheet.Identity.NativeLanguage} não está na lista de {region.Region}.", $"{c.Sheet.Identity.NativeLanguage} isn't on the {region.Region} list."),
                    L.T("O livro permite outra língua se fizer sentido para o personagem.", "The book allows another language if it makes sense for the character."), rules.Document.Lifepath.CustomLanguageAllowed.Reference, "identity.nativeLanguage");
        }
    }

    private void ValidateRoles(Context c)
    {
        var roles = c.Sheet.Roles;
        if (roles.Count == 0)
        {
            c.Error("ROLE_REQUIRED", L.T("Escolha a Role do personagem.", "Pick the character's Role."), null, null, "roles");
            return;
        }
        foreach (var r in roles.Where(r => rules.FindRole(r.Role) is null))
            c.Error("ROLE_UNKNOWN", L.T($"Role desconhecida: {r.Role}.", $"Unknown Role: {r.Role}."), null, null, "roles");
        if (roles.Select(r => r.Role).Distinct(StringComparer.OrdinalIgnoreCase).Count() != roles.Count)
            c.Error("ROLE_DUPLICATED", L.T("A mesma Role aparece duas vezes.", "The same Role appears twice."), null, null, "roles");

        var maxRank = rules.Document.Progression.MaxRank.Value;
        foreach (var r in roles.Where(r => r.Rank < 1 || r.Rank > maxRank))
            c.Error("ROLE_RANK_RANGE", L.T($"Rank de {r.Role} fora de 1–{maxRank}.", $"{r.Role} Rank outside 1–{maxRank}."), null, rules.Document.Progression.MaxRank.Reference, "roles");

        if (!c.Sheet.Creation.Finalized)
        {
            if (roles.Count > 1 && !rules.Document.Creation.MulticlassAtCreation.Value)
                c.Error("MULTICLASS_AT_CREATION", L.T("Não é possível começar com duas Roles.", "You can't start with two Roles."),
                    L.T("Adicione outra Role depois de concluir a criação, com Improvement Points.", "Add another Role after completing creation, with Improvement Points."), rules.Document.Creation.MulticlassAtCreation.Reference, "roles");
            var start = rules.Document.Creation.RoleAbilityStartRank;
            if (roles[0].Rank != start.Value)
                c.Error("ROLE_START_RANK", L.T($"A habilidade de Role começa no Rank {start.Value}.", $"The Role Ability starts at Rank {start.Value}."), null, start.Reference, "roles");
            return;
        }

        // After creation, new Roles only come in through the multiclass command, which checks the Rank 4 rule
        // (CP:R p.142); manual GM adjustments are logged instead.
    }

    // ------------------------------------------------------------ STATs

    private void ValidateStatsShape(Context c)
    {
        foreach (var key in rules.StatOrder)
        {
            if (!c.Sheet.Stats.TryGetValue(key, out var v))
                c.Error("STAT_MISSING", L.T($"{key} sem valor.", $"{key} has no value."), null, null, $"stats.{key}");
            else if (v < 0 || v > TechnicalStatMax)
                c.Error("STAT_INVALID", L.T($"{key} = {v} não é um valor válido.", $"{key} = {v} isn't a valid value."), null, null, $"stats.{key}");
        }
        foreach (var key in c.Sheet.Stats.Keys.Where(k => !rules.StatOrder.Contains(k, StringComparer.OrdinalIgnoreCase)))
            c.Error("STAT_UNKNOWN", $"STAT desconhecido: {key}.", null, null, "stats");
    }

    private void ValidateStreetratStats(Context c)
    {
        var table = Table(c);
        if (table is null) return;
        var source = rules.Document.Creation.Methods.Streetrat.Stats.Reference;
        if (c.Sheet.Creation.TemplateRow is not { } row || row < 1 || row > table.Rows.Length)
        {
            c.Error("TEMPLATE_ROW_REQUIRED", L.T("Role 1d10 ou escolha a linha da tabela de STATs da Role.", "Roll 1d10 or pick the row of the Role's STAT table."), null, source, "creation.templateRow");
            return;
        }
        var values = table.Rows[row - 1];
        for (var i = 0; i < rules.StatOrder.Count; i++)
        {
            var key = rules.StatOrder[i];
            if (c.Sheet.Stat(key) != values[i])
                c.Error("STAT_TEMPLATE_MISMATCH", L.T($"{key} deve ser {values[i]} (linha {row} da tabela {c.Sheet.PrimaryRole!.Role}).", $"{key} must be {values[i]} (row {row} of the {c.Sheet.PrimaryRole!.Role} table)."),
                    L.T("No Streetrat os STATs são copiados da tabela sem trocas.", "In Streetrat the STATs are copied from the table with no swaps."), source, $"stats.{key}");
        }
    }

    private void ValidateEdgerunnerStats(Context c)
    {
        var table = Table(c);
        if (table is null) return;
        var source = rules.Document.Creation.Methods.Edgerunner.Stats.Reference;
        for (var i = 0; i < rules.StatOrder.Count; i++)
        {
            var key = rules.StatOrder[i];
            if (!c.Sheet.Creation.StatRolls.TryGetValue(key, out var roll) || roll < 1 || roll > table.Rows.Length)
            {
                c.Error("STAT_ROLL_REQUIRED", L.T($"Role 1d10 para {key}.", $"Roll 1d10 for {key}."), null, source, $"creation.statRolls.{key}");
                continue;
            }
            var expected = table.Rows[roll - 1][i];
            if (c.Sheet.Stat(key) != expected)
                c.Error("STAT_ROLL_MISMATCH", L.T($"{key} deve ser {expected} (rolagem {roll} na coluna {key}).", $"{key} must be {expected} (roll of {roll} in the {key} column)."), null, source, $"stats.{key}");
        }
    }

    private CreationBudget? ValidatePointBuyStats(Context c)
    {
        var l = c.Limits;
        if (l.StatPoints is null || l.StatMin is null || l.StatMax is null) return null;
        foreach (var key in rules.StatOrder)
        {
            var v = c.Sheet.Stat(key);
            if (v < l.StatMin.Value)
                c.Error("STAT_BELOW_MIN", L.T($"{key} = {v}; o mínimo é {l.StatMin.Value}.", $"{key} = {v}; the minimum is {l.StatMin.Value}."), null, l.StatMin.Source, $"stats.{key}");
            if (v > l.StatMax.Value)
                c.Error("STAT_ABOVE_MAX", L.T($"{key} = {v}; o máximo é {l.StatMax.Value}.", $"{key} = {v}; the maximum is {l.StatMax.Value}."), null, l.StatMax.Source, $"stats.{key}");
        }
        var used = CreationCosts.StatPoints(rules, c.Sheet);
        var budget = new CreationBudget(used, l.StatPoints.Value, l.StatPoints.Source, l.StatPoints.CampaignVariant);
        if (budget.Exceeded)
            c.Error("STAT_BUDGET_EXCEEDED", L.T($"{used} pontos de STAT gastos; o limite é {budget.Total}.", $"{used} STAT points spent; the limit is {budget.Total}."),
                $"Tire {used - budget.Total} ponto(s).", budget.Source, "stats");
        else if (budget.Remaining > 0)
            c.Warn("STAT_BUDGET_UNSPENT", L.T($"Ainda há {budget.Remaining} ponto(s) de STAT para distribuir.", $"There are still {budget.Remaining} STAT point(s) to spend."), null, budget.Source, "stats");
        return budget;
    }

    private StatTable? Table(Context c) =>
        c.Sheet.PrimaryRole is { } r ? rules.FindRole(r.Role)?.StatTable : null;

    // ------------------------------------------------------------ skills

    private void ValidateSkillsShape(Context c)
    {
        var maxInput = rules.Shared.Document.SkillCost.MaxLevelInput.Value;
        foreach (var (key, level) in c.Sheet.Skills)
        {
            var (id, spec) = SkillKey.Parse(key);
            var def = rules.FindSkill(id);
            if (def is null)
            {
                c.Error("SKILL_UNKNOWN", L.T($"Perícia desconhecida: {key}.", $"Unknown skill: {key}."), null, null, $"skills.{key}");
                continue;
            }
            if (spec == CharacterRuleBook.ChooseToken)
                c.Error("SKILL_CHOICE_REQUIRED", L.T($"Escolha a especialização de {def.Name}.", $"Pick the {def.Name} specialization."), null, null, $"skills.{key}");
            else if (def.Specialized && spec is null)
                c.Error("SKILL_SPECIALIZATION_REQUIRED", L.T($"{def.Name} precisa de especialização.", $"{def.Name} needs a specialization."), null, $"CP:R p.{def.Page}", $"skills.{key}");
            if (level < 0 || level > maxInput)
                c.Error("SKILL_LEVEL_INVALID", L.T($"{key} no nível {level}; o máximo é {maxInput}.", $"{key} at level {level}; the maximum is {maxInput}."), null,
                    rules.Shared.Document.SkillCost.MaxLevelInput.Reference, $"skills.{key}");
        }
    }

    /// <summary>Streetrat: every level comes from the Role template (CP:R p.86–87) plus the native language.</summary>
    private void ValidateTemplateSkills(Context c)
    {
        if (c.Sheet.PrimaryRole is not { } role || rules.FindRole(role.Role) is null) return;
        var source = rules.Document.Creation.Methods.Streetrat.Skills.Reference;
        var expected = new Dictionary<string, int>(
            rules.RoleSkillLevels(role.Role, c.Sheet.Identity.Home, c.Sheet.Creation.SkillChoices), StringComparer.OrdinalIgnoreCase);
        if (CreationCosts.NativeLanguageKey(c.Sheet) is { } native)
            expected[native] = Math.Max(expected.GetValueOrDefault(native), rules.Document.Creation.OriginLanguageLevel.Value);

        foreach (var (key, level) in expected)
            if (c.Sheet.Skills.GetValueOrDefault(key) != level)
                c.Error("SKILL_TEMPLATE_MISMATCH", L.T($"{key} deve estar no nível {level}.", $"{key} must be at level {level}."), L.T("No Streetrat as perícias vêm do template.", "In Streetrat the skills come from the template."), source, $"skills.{key}");
        foreach (var key in c.Sheet.Skills.Keys.Where(k => !expected.ContainsKey(k)))
            c.Error("SKILL_NOT_IN_TEMPLATE", L.T($"{key} não faz parte do template da Role.", $"{key} isn't part of the Role template."), null, source, $"skills.{key}");
    }

    /// <summary>Edgerunner (CP:R p.88) and Complete Package (CP:R p.90) skill points.</summary>
    private CreationBudget? ValidatePointBuySkills(Context c)
    {
        var l = c.Limits;
        if (l.SkillPoints is null || l.SkillMax is null) return null;
        var home = c.Sheet.Identity.Home;
        var native = CreationCosts.NativeLanguageKey(c.Sheet);
        var basic = rules.BasicSkillKeys(home);

        HashSet<string>? allowed = null;
        if (l.RoleSkillsOnly && c.Sheet.PrimaryRole is { } role && rules.FindRole(role.Role) is not null)
        {
            allowed = rules.RoleSkillLevels(role.Role, home, c.Sheet.Creation.SkillChoices).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var source = rules.Document.Creation.Methods.Edgerunner.RoleSkillsOnly.Reference;
            foreach (var key in c.Sheet.Skills.Keys.Where(k => !allowed.Contains(k) && !k.Equals(native, StringComparison.OrdinalIgnoreCase)))
                c.Error("SKILL_NOT_IN_ROLE", L.T($"{key} não é uma perícia da Role {role.Role}.", $"{key} isn't a {role.Role} Role skill."), L.T("No Edgerunner só as 20 perícias da Role recebem pontos.", "In Edgerunner only the 20 Role skills get points."), source, $"skills.{key}");
            if (native is not null && c.Sheet.Skills.GetValueOrDefault(native) > rules.Document.Creation.OriginLanguageLevel.Value && !allowed.Contains(native))
                c.Error("SKILL_NOT_IN_ROLE", L.T($"No Edgerunner o idioma de origem fica no nível {rules.Document.Creation.OriginLanguageLevel.Value}.", $"In Edgerunner the native language stays at level {rules.Document.Creation.OriginLanguageLevel.Value}."),
                    null, source, $"skills.{native}");
        }

        foreach (var (key, level) in c.Sheet.Skills)
        {
            var isNative = key.Equals(native, StringComparison.OrdinalIgnoreCase);
            if (level > l.SkillMax.Value && !(isNative && level <= rules.Document.Creation.OriginLanguageLevel.Value))
                c.Error("SKILL_ABOVE_MAX", L.T($"{key} no nível {level}; o máximo na criação é {l.SkillMax.Value}.", $"{key} at level {level}; the maximum at creation is {l.SkillMax.Value}."), null, l.SkillMax.Source, $"skills.{key}");
        }

        // Every Role skill must be at least the method minimum (Edgerunner), and the Basic Skills at least 2 (both).
        if (allowed is not null && l.SkillMin is not null)
            foreach (var key in allowed.Where(k => c.Sheet.Skills.GetValueOrDefault(k) < l.SkillMin.Value))
                c.Error("SKILL_BELOW_MIN", L.T($"{key} precisa estar no nível {l.SkillMin.Value} ou mais.", $"{key} must be at level {l.SkillMin.Value} or more."), null, l.SkillMin.Source, $"skills.{key}");
        foreach (var key in basic.Where(k => c.Sheet.Skills.GetValueOrDefault(k) < l.BasicSkillMin.Value))
            if (allowed is null || !allowed.Contains(key) || l.SkillMin is null)
                c.Error("BASIC_SKILL_BELOW_MIN", L.T($"Perícia básica {key} precisa estar no nível {l.BasicSkillMin.Value} ou mais.", $"Basic skill {key} must be at level {l.BasicSkillMin.Value} or more."), null, l.BasicSkillMin.Source, $"skills.{key}");

        var used = CreationCosts.SkillPoints(rules, c.Sheet);
        var budget = new CreationBudget(used, l.SkillPoints.Value, l.SkillPoints.Source, l.SkillPoints.CampaignVariant);
        if (budget.Exceeded)
            c.Error("SKILL_BUDGET_EXCEEDED", L.T($"{used} pontos de perícia gastos; o limite é {budget.Total}.", $"{used} skill points spent; the limit is {budget.Total}."),
                L.T($"Tire {used - budget.Total} ponto(s). Perícias (x2) custam 2 por nível.", $"Remove {used - budget.Total} point(s). (x2) skills cost 2 per level."), budget.Source, "skills");
        else if (budget.Remaining > 0)
            c.Warn("SKILL_BUDGET_UNSPENT", L.T($"Ainda há {budget.Remaining} ponto(s) de perícia para distribuir.", $"There are still {budget.Remaining} skill point(s) to spend."), null, budget.Source, "skills");
        return budget;
    }

    // ------------------------------------------------------------ starting package and money

    /// <summary>Streetrat and Edgerunner: one option picked for every "X or Y" group (CP:R p.98, 103, 117).</summary>
    private void ValidateStartingChoices(Context c)
    {
        if (c.Sheet.PrimaryRole is not { } role || rules.FindRole(role.Role) is not { } rr) return;
        foreach (var (name, section) in new[] { ("weapons", rr.Starting.Weapons), ("outfit", rr.Starting.Outfit), ("cyberware", rr.Starting.Cyberware) })
            for (var g = 0; g < section.Groups.Length; g++)
            {
                if (section.Groups[g].Length < 2) continue;
                var key = $"{name}:{g}";
                if (!c.Sheet.Creation.StartingChoices.TryGetValue(key, out var pick) || pick < 0 || pick >= section.Groups[g].Length)
                    c.Error("STARTING_CHOICE_REQUIRED", L.T($"Escolha uma das opções do equipamento inicial ({Describe(section.Groups[g], c.Catalog)}).", $"Pick one of the starting gear options ({Describe(section.Groups[g], c.Catalog)})."),
                        L.T("Escolha uma e apenas uma opção.", "Pick one and only one option."), section.Source, $"creation.startingChoices.{key}");
            }
    }

    private static string Describe(StartingItem[][] options, NpcCatalog catalog) =>
        string.Join(L.T(" ou ", " or "), options.Select(o => string.Join(" + ", o.Select(i => catalog.Find(i.Slug)?.Name ?? i.Slug))));

    /// <summary>Complete Package: 2,550eb for anything and 800eb only for Fashion and Fashionware (CP:R p.104–105).</summary>
    private (CreationBudget? Gear, CreationBudget? Fashion) ValidateCreationBudgets(Context c)
    {
        var l = c.Limits;
        if (l.GearBudget is null || l.FashionBudget is null) return (null, null);

        var spent = c.Sheet.Inventory.Select(e => (e.PaidFrom, e.PricePaid, e.ItemId))
            .Concat(c.Sheet.Cyberware.Select(e => (e.PaidFrom, e.PricePaid, e.ItemId)))
            .ToList();
        var gearUsed = spent.Where(s => s.PaidFrom == BudgetPool.CreationGear).Sum(s => s.PricePaid ?? 0);
        var fashionUsed = spent.Where(s => s.PaidFrom == BudgetPool.CreationFashion).Sum(s => s.PricePaid ?? 0);

        foreach (var s in spent.Where(s => s.PaidFrom == BudgetPool.CreationFashion))
            if (c.Catalog.Find(s.ItemId) is { } item && !IsFashion(item))
                c.Error("FASHION_BUDGET_MISUSE", L.T($"{item.Name} não é Fashion nem Fashionware e não pode sair do orçamento de moda.", $"{item.Name} is neither Fashion nor Fashionware and can't come out of the fashion budget."),
                    L.T("Pague com o orçamento geral.", "Pay with the general budget."), rules.Document.Creation.Methods.CompletePackage.FashionBudget.Reference, "inventory");

        var gear = new CreationBudget(gearUsed, l.GearBudget.Value, l.GearBudget.Source, l.GearBudget.CampaignVariant);
        var fashion = new CreationBudget(fashionUsed, l.FashionBudget.Value, l.FashionBudget.Source, l.FashionBudget.CampaignVariant);
        if (gear.Exceeded)
            c.Error("GEAR_BUDGET_EXCEEDED", L.T($"{Eb(gearUsed)} gastos do orçamento geral de {Eb(gear.Total)}.", $"{Eb(gearUsed)} spent of the {Eb(gear.Total)} general budget."), null, gear.Source, "inventory");
        if (fashion.Exceeded)
            c.Error("FASHION_BUDGET_EXCEEDED", L.T($"{Eb(fashionUsed)} gastos do orçamento de moda de {Eb(fashion.Total)}.", $"{Eb(fashionUsed)} spent of the {Eb(fashion.Total)} fashion budget."), null, fashion.Source, "inventory");
        else if (fashion.Remaining > 0 && !rules.Document.Creation.Methods.CompletePackage.UnspentFashionKept.Value)
            c.Info("FASHION_BUDGET_UNSPENT", L.T($"{Eb(fashion.Remaining)} do orçamento de moda sobram e serão perdidos ao concluir.", $"{Eb(fashion.Remaining)} of the fashion budget are left and will be lost on completion."),
                null, rules.Document.Creation.Methods.CompletePackage.UnspentFashionKept.Reference, "inventory");
        return (gear, fashion);
    }

    /// <summary>Clothing (Fashion, CP:R p.102) or Fashionware (CP:R p.111).</summary>
    public static bool IsFashion(CatalogItem item) =>
        item.Armor?.Type == ArmorType.Clothing || item.Cyber?.IsFashionware == true;

    private static void ValidateWallet(Context c)
    {
        if (c.Sheet.Wallet.Balance < 0)
            c.Error("BALANCE_NEGATIVE", L.T("O saldo está negativo.", "The balance is negative."), null, null, "wallet.balance");
        foreach (var e in c.Sheet.Inventory.Where(e => c.Catalog.Find(e.ItemId) is null))
            c.Error("ITEM_MISSING", L.T($"Item {e.ItemId} do inventário não existe mais no catálogo.", $"Inventory item {e.ItemId} no longer exists in the catalog."), null, null, "inventory");
        foreach (var e in c.Sheet.Cyberware.Where(e => c.Catalog.Find(e.ItemId) is null))
            c.Error("ITEM_MISSING", L.T($"Implante {e.ItemId} não existe mais no catálogo.", $"Implant {e.ItemId} no longer exists in the catalog."), null, null, "cyberware");
    }

    public static string Eb(int value) => $"{value:N0}eb".Replace(',', '.');

    private sealed class Context(CharacterSheet sheet, NpcCatalog catalog, CreationLimits limits)
    {
        public CharacterSheet Sheet { get; } = sheet;
        public NpcCatalog Catalog { get; } = catalog;
        public CreationLimits Limits { get; } = limits;
        public List<CharacterIssue> Issues { get; } = [];

        public void Error(string code, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new CharacterIssue(code, CharacterIssueSeverity.Error, message, suggestion, source, field));

        public void Warn(string code, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new CharacterIssue(code, CharacterIssueSeverity.Warning, message, suggestion, source, field));

        public void Info(string code, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new CharacterIssue(code, CharacterIssueSeverity.Info, message, suggestion, source, field));
    }
}
