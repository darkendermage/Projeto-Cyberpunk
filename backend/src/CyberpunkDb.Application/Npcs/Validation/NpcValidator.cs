using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Npcs.Validation;

/// <summary>
/// The single validation layer for every NPC, whatever the creation mode. Pure: takes the sheet and a catalog
/// snapshot, computes budgets and derived values, and lists every violation with its source and a suggestion.
/// </summary>
public sealed class NpcValidator(NpcRuleBook rules)
{
    private readonly NpcMechanics _m = new(rules);

    public NpcValidationReport Validate(NpcSheet input, NpcCatalog catalog)
    {
        var sheet = input.Normalize();
        var t = rules.Template(sheet.Level, sheet.Hardened);
        var ctx = new Context(sheet, t, catalog);

        ValidateIdentity(ctx);
        var stats = ValidateStats(ctx);
        var (gearLines, gearBudget) = ValidateGear(ctx);
        var (cyberLines, cyberBudget) = ValidateCyberware(ctx);
        var derived = ComputeDerived(ctx, cyberLines);
        var (skillLines, skillBudget) = ValidateSkills(ctx, derived);
        ValidateWeapons(ctx, derived);
        ValidateDerived(ctx, derived);
        ValidateRole(ctx);

        var issues = ApplyExceptions(ctx);
        var isValid = !issues.Any(i => i.Severity == NpcIssueSeverity.Error && !i.Overridden);
        var status = !isValid ? NpcStatus.Draft
            : issues.Any(i => i.Overridden) ? NpcStatus.ValidWithExceptions
            : NpcStatus.Valid;

        return new NpcValidationReport(isValid, status, Summary(t), stats, skillBudget, gearBudget, cyberBudget, derived,
            skillLines, gearLines, cyberLines, issues);
    }

    public TemplateSummary Summary(NpcTemplate t)
    {
        var role = t.HardenedRoleRank?.Value ?? t.RoleRank.Value;
        return new TemplateSummary(t.Level, t.Hardened, t.DisplayName, t.StatPoints.Value, t.MaxStat.Value, t.SkillPoints.Value,
            t.MaxSkill.Value, t.SuggestedHp.Value.ToString(), t.SuggestedSp.Value.ToString(), t.CombatSkillBase.Value.ToString(),
            t.NonCombatSkillBase.Value.ToString(), t.GearBudget.Value, t.CyberwareBudget.Value, t.WeaponQuality.Value.ToString(),
            role.Rank, rules.Document.RolePresence.GetValueOrDefault(role.Presence)?.Label ?? role.Presence.ToString());
    }

    // ------------------------------------------------------------ identity and STATs

    private void ValidateIdentity(Context c)
    {
        if (c.Sheet.Name.Length == 0)
            c.Warn("NAME_MISSING", "NAME_MISSING", L.T("O NPC está sem nome.", "The NPC has no name."), L.T("Dê um nome ou apelido antes de salvar.", "Give a name or nickname before saving."), null, "name");
        if (c.Sheet.Role is { } role && !rules.Document.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            c.Hard("ROLE_UNKNOWN", "ROLE_UNKNOWN", L.T($"Role \"{role}\" não existe.", $"Role \"{role}\" doesn't exist."), L.T($"Use um destes: {string.Join(", ", rules.Document.Roles)}.", $"Use one of these: {string.Join(", ", rules.Document.Roles)}."), null, "role");
        if (c.Sheet.GangId is { } gang && rules.FindGang(gang) is null)
            c.Info("GANG_UNKNOWN", "GANG_UNKNOWN", L.T($"A gangue \"{gang}\" não está na lista de gangues.", $"The gang \"{gang}\" isn't on the gang list."), L.T("Escolha outra gangue ou deixe sem gangue.", "Pick another gang or leave it without one."), null, "gangId");
        if (c.Sheet.RoleRank is < 0 or > 10)
            c.Hard("ROLE_RANK_INVALID", "ROLE_RANK_INVALID", L.T("O Rank da Role Ability deve estar entre 0 e 10.", "The Role Ability Rank must be between 0 and 10."), null, null, "roleRank");
        else if (c.Sheet.RoleRank > 0 && c.Sheet.Role is null)
            c.Hard("ROLE_RANK_WITHOUT_ROLE", "ROLE_RANK_WITHOUT_ROLE", L.T("Há Rank de Role Ability sem Role escolhida.", "There's a Role Ability Rank with no Role chosen."), L.T("Escolha a Role ou zere o Rank.", "Pick the Role or set the Rank to zero."), null, "role");
    }

    private BudgetLine ValidateStats(Context c)
    {
        var s = rules.Document.Stats;
        foreach (var key in c.Sheet.Stats.Keys.Where(k => !rules.StatKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
            c.Hard("STAT_UNKNOWN", $"STAT_UNKNOWN:{key}", L.T($"STAT \"{key}\" não faz parte do orçamento de NPC.", $"STAT \"{key}\" isn't part of the NPC budget."),
                key.Equals("LUCK", StringComparison.OrdinalIgnoreCase) ? L.T("LUCK fica no campo próprio, fora do orçamento.", "LUCK goes in its own field, outside the budget.") : null, s.Luck.Source, $"stats.{key}");

        var used = 0;
        foreach (var key in rules.StatKeys)
        {
            if (!c.Sheet.Stats.TryGetValue(key, out var v))
            {
                c.Hard("STAT_MISSING", $"STAT_MISSING:{key}", L.T($"Informe o valor de {key}.", $"Enter the {key} value."), null, null, $"stats.{key}");
                continue;
            }
            if (v < 0 || v > s.InputMax.Value)
            {
                c.Hard("STAT_INVALID", $"STAT_INVALID:{key}", L.T($"{key} {v} está fora do intervalo aceito (0 a {s.InputMax.Value}).", $"{key} {v} is outside the accepted range (0 to {s.InputMax.Value})."), null, null, $"stats.{key}");
                continue;
            }
            used += v;
            var min = key == "EMP" ? s.EmpMin.Value : s.Min.Value;
            if (v < min)
                c.Error("STAT_BELOW_MIN", $"STAT_BELOW_MIN:{key}", L.T($"{key} {v} está abaixo do mínimo de {min}.", $"{key} {v} is below the minimum of {min}."),
                    L.T($"Aumente {key} para {min}.", $"Raise {key} to {min}."), s.Min.Source, $"stats.{key}");
            if (v > c.T.MaxStat.Value)
                c.Error("STAT_ABOVE_MAX", $"STAT_ABOVE_MAX:{key}", L.T($"{key} {v} ultrapassa o máximo de {c.T.MaxStat.Value} para {c.T.DisplayName}.", $"{key} {v} goes over the maximum of {c.T.MaxStat.Value} for {c.T.DisplayName}."),
                    L.T($"Reduza {key} para {c.T.MaxStat.Value}", $"Lower {key} to {c.T.MaxStat.Value}") + (key == "BODY" ? L.T(" ou use um Implanted Linear Frame.", " or use an Implanted Linear Frame.") : "."), c.T.MaxStat.Reference, $"stats.{key}");
        }

        var budget = c.T.StatPoints.Value;
        if (used > budget)
            c.Error("STAT_BUDGET_EXCEEDED", "STAT_BUDGET_EXCEEDED",
                L.T($"O NPC ultrapassa o orçamento de STATs em {Points(used - budget)}.", $"The NPC goes {Points(used - budget)} over the STAT budget."),
                L.T($"Remova {Points(used - budget)} distribuídos entre os STATs (orçamento de {budget} para {c.T.DisplayName}).", $"Remove {Points(used - budget)} spread across the STATs (budget of {budget} for {c.T.DisplayName})."),
                c.T.StatPoints.Reference, "stats");
        else if (used < budget)
            c.Info("STAT_BUDGET_UNSPENT", "STAT_BUDGET_UNSPENT", L.T($"Restam {Points(budget - used)} de STAT para distribuir.", $"{Points(budget - used)} of STAT left to spend."), null, c.T.StatPoints.Reference, "stats");

        if (c.Sheet.Luck is > 0)
            c.Info("LUCK_PRESENT", "LUCK_PRESENT", L.T($"LUCK {c.Sheet.Luck} fica fora do orçamento de STATs. NPCs raramente têm LUCK.", $"LUCK {c.Sheet.Luck} stays outside the STAT budget. NPCs rarely have LUCK."), null, s.Luck.Source, "luck");

        return new BudgetLine(used, budget, c.T.StatPoints.Reference);
    }

    // ------------------------------------------------------------ gear

    private (List<GearLine>, BudgetLine) ValidateGear(Context c)
    {
        var lines = new List<GearLine>();
        var total = 0;
        for (var i = 0; i < c.Sheet.Gear.Count; i++)
        {
            var g = c.Sheet.Gear[i];
            var field = $"gear[{i}]";
            if (c.Catalog.Find(g.ItemId) is not { } item)
            {
                c.Hard("ITEM_NOT_FOUND", $"ITEM_NOT_FOUND:gear:{i}", L.T($"Item {g.ItemId} não existe no catálogo.", $"Item {g.ItemId} doesn't exist in the catalog."), L.T("Remova o item da ficha.", "Remove the item from the sheet."), null, field);
                continue;
            }
            if (item.IsCyberwareBudget)
            {
                c.Hard("ITEM_WRONG_LIST", $"ITEM_WRONG_LIST:gear:{i}", L.T($"{item.Name} é cyberware e entra na seção de cyberware.", $"{item.Name} is cyberware and goes in the cyberware section."), L.T("Mova o item para Cyberware.", "Move the item to Cyberware."), null, field);
                continue;
            }
            if (g.Quantity is < 1 or > 999)
                c.Hard("ITEM_QUANTITY_INVALID", $"ITEM_QUANTITY_INVALID:{i}", L.T($"Quantidade de {item.Name} deve estar entre 1 e 999.", $"Quantity of {item.Name} must be between 1 and 999."), null, null, field);

            var (unit, quality, qualityError) = _m.GearUnitPrice(item, g.Quality);
            if (g.Quality is not null && item.Weapon is null)
                c.Hard("QUALITY_NOT_WEAPON", $"QUALITY_NOT_WEAPON:{i}", L.T($"Qualidade só se aplica a armas ({item.Name}).", $"Quality only applies to weapons ({item.Name})."), null, null, field);
            if (qualityError is not null)
                c.Hard("WEAPON_QUALITY_UNAVAILABLE", $"WEAPON_QUALITY_UNAVAILABLE:{i}", qualityError, L.T("Mantenha a qualidade cadastrada ou escolha outra arma.", "Keep the catalog quality or pick another weapon."),
                    rules.Document.WeaponQualityPricing.Source, field);
            if (unit is null && qualityError is null)
                c.Warn("ITEM_PRICE_UNKNOWN", $"ITEM_PRICE_UNKNOWN:{i}", L.T($"{item.Name} não tem preço fixo no catálogo; não entra no orçamento.", $"{item.Name} has no fixed catalog price; it doesn't count against the budget."), L.T("Confira o preço com o Mestre.", "Check the price with the GM."), null, field);

            if (g.Worn is { } slot)
            {
                if (item.Armor is not { Type: ArmorType.BodyArmor })
                    c.Hard("ARMOR_NOT_WEARABLE", $"ARMOR_NOT_WEARABLE:{i}", L.T($"{item.Name} não é uma armadura que se veste no corpo ou na cabeça.", $"{item.Name} isn't armor worn on the body or head."), L.T("Deixe o item apenas como carregado.", "Keep the item as carried only."), null, field);
                else if (g.Quantity != 1)
                    c.Hard("ARMOR_WORN_QUANTITY", $"ARMOR_WORN_QUANTITY:{i}", L.T($"Cada armadura vestida corresponde a uma peça ({item.Name}).", $"Each worn armor is one piece ({item.Name})."), L.T("Use quantidade 1 e adicione outra linha para o outro local.", "Use quantity 1 and add another line for the other location."), null, field);
            }

            var lineTotal = (unit ?? 0) * Math.Max(0, g.Quantity);
            total += lineTotal;
            lines.Add(new GearLine(i, item.Id, item.Name, item.Slug, item.Category, g.Quantity, item.Weapon is null ? null : quality,
                unit, lineTotal, g.Worn, GearSummary(item), g.Locked));
        }

        foreach (var group in lines.Where(l => l.Worn is not null).GroupBy(l => l.Worn!.Value).Where(g => g.Count() > 1))
            c.Error("ARMOR_SLOT_CONFLICT", $"ARMOR_SLOT_CONFLICT:{group.Key}",
                L.T($"Há {group.Count()} armaduras vestidas {(group.Key == ArmorSlot.Body ? "no corpo" : "na cabeça")}: {string.Join(", ", group.Select(l => l.Name))}.", $"There are {group.Count()} armors worn on the {(group.Key == ArmorSlot.Body ? "body" : "head")}: {string.Join(", ", group.Select(l => l.Name))}."),
                L.T("Vista só uma por local; as outras ficam carregadas.", "Wear only one per location; the others stay carried."), rules.Document.Armor.OneArmorPerLocation.Reference, "gear");

        var budget = c.T.GearBudget.Value;
        if (total > budget)
            c.Error("GEAR_BUDGET_EXCEEDED", "GEAR_BUDGET_EXCEEDED",
                L.T($"O equipamento custa {Eb(total)} e ultrapassa o orçamento de {Eb(budget)} em {Eb(total - budget)}.", $"The gear costs {Eb(total)} and goes {Eb(total - budget)} over the budget of {Eb(budget)}."),
                L.T("Troque por itens mais baratos, use Poor Quality ou remova itens.", "Swap for cheaper items, use Poor Quality or remove items."), c.T.GearBudget.Reference, "gear");
        return (lines, new BudgetLine(total, budget, c.T.GearBudget.Reference));
    }

    private static string? GearSummary(CatalogItem item) => item switch
    {
        { Weapon: { } w } => $"{w.Damage} · ROF {w.Rof}" + (w.Exotic ? L.T(" · exótica", " · exotic") : ""),
        { Armor: { Sp: { } sp } a } => $"SP {sp}" + (a.RefPenalty > 0 ? $" · -{a.RefPenalty} REF/DEX/MOVE" : ""),
        { Armor: { BodyValue: { } b } } => $"BODY {b}",
        { Ammo: { } a } => a.PerPack is { } n ? L.T($"pacote com {n}", $"pack of {n}") : null,
        _ => null,
    };

    // ------------------------------------------------------------ cyberware

    private (List<CyberLine>, BudgetLine) ValidateCyberware(Context c)
    {
        var entries = c.Sheet.Cyberware;
        var byInstance = new Dictionary<string, (NpcCyberwareEntry Entry, CatalogItem Item)>(StringComparer.Ordinal);
        var lines = new List<CyberLine>();
        var total = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var field = $"cyberware[{i}]";
            if (string.IsNullOrWhiteSpace(e.InstanceId) || byInstance.ContainsKey(e.InstanceId))
            {
                c.Hard("CYBER_INSTANCE_INVALID", $"CYBER_INSTANCE_INVALID:{i}", L.T("Cada implante precisa de um identificador único.", "Every implant needs a unique identifier."), null, null, field);
                continue;
            }
            if (c.Catalog.Find(e.ItemId) is not { } item)
            {
                c.Hard("ITEM_NOT_FOUND", $"ITEM_NOT_FOUND:cyber:{e.InstanceId}", L.T($"Item {e.ItemId} não existe no catálogo.", $"Item {e.ItemId} doesn't exist in the catalog."), L.T("Remova o implante da ficha.", "Remove the implant from the sheet."), null, field);
                continue;
            }
            if (!item.IsCyberwareBudget)
            {
                c.Hard("ITEM_WRONG_LIST", $"ITEM_WRONG_LIST:cyber:{e.InstanceId}", L.T($"{item.Name} não é cyberware.", $"{item.Name} isn't cyberware."), L.T("Mova o item para Equipamentos.", "Move the item to Gear."), null, field);
                continue;
            }
            byInstance[e.InstanceId] = (e, item);
        }

        // Slots used inside each parent and in each standalone pool.
        var usedInParent = new Dictionary<string, int>(StringComparer.Ordinal);
        var usedInPool = new Dictionary<string, int>(StringComparer.Ordinal);
        var assumedReported = new HashSet<int>();

        foreach (var (id, (e, item)) in byInstance)
        {
            var field = $"cyberware[{entries.IndexOf(e)}]";
            var (slots, assumed) = _m.SlotsRequired(item);
            var required = item.Cyber!.RequiresItemId is { } rid ? c.Catalog.Find(rid) : null;
            string? pool = null;

            if (e.ParentInstanceId is { } pid)
            {
                if (!byInstance.TryGetValue(pid, out var parent))
                    c.Hard("CYBER_PARENT_MISSING", $"CYBER_PARENT_MISSING:{id}", L.T($"{item.Name} aponta para uma peça base que não está na ficha.", $"{item.Name} points to a foundational piece that isn't on the sheet."),
                        required is null ? L.T("Escolha onde ele está instalado.", "Pick where it's installed.") : $"Adicione {required.Name} e instale {item.Name} nele.", rules.Document.Cyberware.OptionSlotDefault.Source, field);
                else if (parent.Item.Cyber is not { Foundational: true })
                    c.Hard("CYBER_PARENT_INVALID", $"CYBER_PARENT_INVALID:{id}", L.T($"{parent.Item.Name} não é uma peça base com Option Slots.", $"{parent.Item.Name} isn't a foundational piece with Option Slots."), null, rules.Document.Cyberware.OptionSlotDefault.Source, field);
                else if (required is not null && !NpcMechanics.ParentCompatible(parent.Item, required))
                    c.Hard("CYBER_PARENT_WRONG", $"CYBER_PARENT_WRONG:{id}", L.T($"{item.Name} precisa ser instalado em {required.Name}, não em {parent.Item.Name}.", $"{item.Name} must be installed in {required.Name}, not in {parent.Item.Name}."), null, null, field);
                else
                    usedInParent[pid] = usedInParent.GetValueOrDefault(pid) + slots;
            }
            else if (required is not null)
            {
                c.Hard("CYBER_PARENT_MISSING", $"CYBER_PARENT_MISSING:{id}", L.T($"{item.Name} exige {required.Name} como peça base.", $"{item.Name} needs {required.Name} as its foundational piece."),
                    $"Adicione {required.Name} e instale {item.Name} nele.", rules.Document.Cyberware.OptionSlotDefault.Source, field);
            }
            else if (_m.PoolFor(item) is { } p)
            {
                pool = p.Key;
                usedInPool[p.Key] = usedInPool.GetValueOrDefault(p.Key) + slots;
            }

            if (assumed && (e.ParentInstanceId is not null || pool is not null) && assumedReported.Add(item.Id))
                c.Info("CYBER_SLOTS_ASSUMED", $"CYBER_SLOTS_ASSUMED:{item.Id}", L.T($"{item.Name} não informa quantos slots ocupa; contado como 1.", $"{item.Name} doesn't say how many slots it takes; counted as 1."), null,
                    rules.Document.Cyberware.OptionSlotDefault.Source, field);

            var freeText = _m.FreeTextRequirement(item, c.Catalog);
            if (freeText is not null)
                c.Warn("CYBER_REQUIREMENT_TEXT", $"CYBER_REQUIREMENT_TEXT:{id}", L.T($"{item.Name} tem pré-requisito que o sistema não verifica: \"{freeText}\".", $"{item.Name} has a prerequisite the system doesn't check: \"{L.D(freeText)}\"."),
                    L.T("Confira manualmente com o livro.", "Check it manually against the book."), item.SourceBook is null ? null : $"{item.SourceBook}{(item.Page is { } pg ? $" p.{pg}" : "")}", field);

            if (item.Price is null)
                c.Warn("ITEM_PRICE_UNKNOWN", $"ITEM_PRICE_UNKNOWN:cyber:{id}", L.T($"{item.Name} não tem preço fixo no catálogo; não entra no orçamento.", $"{item.Name} has no fixed catalog price; it doesn't count against the budget."), null, null, field);
            total += item.Price ?? 0;

            lines.Add(new CyberLine(id, item.Id, item.Name, item.Slug, item.Category, e.ParentInstanceId, item.Price, slots, assumed,
                item.Cyber.Foundational ? _m.SlotsProvided(item) : null, null, pool, item.Cyber.Foundational,
                item.Cyber.RequiresItemId, item.Cyber.RequiresPair, freeText, e.Locked));
        }

        // Option Slots of each foundational piece.
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Foundational) continue;
            var used = usedInParent.GetValueOrDefault(line.InstanceId);
            if (line.SlotsProvided is { } provided)
            {
                lines[i] = line with { SlotsFree = provided - used };
                if (used > provided)
                    c.Hard("CYBER_SLOTS_EXCEEDED", $"CYBER_SLOTS_EXCEEDED:{line.InstanceId}",
                        L.T($"{line.Name} tem {provided} Option Slots, mas as opções instaladas ocupam {used}.", $"{line.Name} has {provided} Option Slots, but the installed options take {used}."),
                        L.T($"Remova opções ou instale uma segunda peça base.", $"Remove options or install a second foundational piece."), rules.Document.Cyberware.OptionSlotDefault.Source, "cyberware");
            }
        }

        foreach (var pool in rules.Document.Cyberware.Pools)
        {
            var used = usedInPool.GetValueOrDefault(pool.Key);
            if (used > pool.Slots)
                c.Hard("CYBER_POOL_EXCEEDED", $"CYBER_POOL_EXCEEDED:{pool.Key}",
                    L.T($"{pool.Label} comporta {pool.Slots} slots; a ficha usa {used}.", $"{pool.Label} holds {pool.Slots} slots; the sheet uses {used}."), L.T("Remova implantes dessa categoria.", "Remove implants from that category."), pool.Source, "cyberware");
        }

        // Body capacity (two eyes, one Cyberaudio Suite...).
        foreach (var rule in rules.Document.Cyberware.Capacity)
        {
            var matching = byInstance.Values.Where(x => NpcMechanics.MatchesCapacity(rule, x.Item)).ToList();
            if (matching.Count > rule.Max)
                c.Hard("CYBER_CAPACITY_EXCEEDED", $"CYBER_CAPACITY_EXCEEDED:{rule.Label}",
                    L.T($"{rule.Label}: no máximo {rule.Max}, a ficha tem {matching.Count} ({string.Join(", ", matching.Select(x => x.Item.Name))}).", $"{rule.Label}: at most {rule.Max}, the sheet has {matching.Count} ({string.Join(", ", matching.Select(x => x.Item.Name))})."),
                    "Remova o excedente.", rule.Reference, "cyberware");
        }

        foreach (var group in rules.Document.Cyberware.ExclusiveGroups)
        {
            var matching = byInstance.Values.Where(x => group.Slugs.Contains(x.Item.Slug, StringComparer.OrdinalIgnoreCase)).ToList();
            if (matching.Count > group.Max)
                c.Hard("CYBER_INCOMPATIBLE", $"CYBER_INCOMPATIBLE:{group.Label}",
                    L.T($"A combinação de implantes selecionada possui incompatibilidades: só {group.Max} {group.Label} por vez ({string.Join(", ", matching.Select(x => x.Item.Name))}).", $"The selected implant combination has conflicts: only {group.Max} {group.Label} at a time ({string.Join(", ", matching.Select(x => x.Item.Name))})."),
                    L.T($"Mantenha apenas um {group.Label}.", $"Keep only one {group.Label}."), group.Source, "cyberware");
        }

        // Paired options: one in each of two different foundational pieces.
        foreach (var group in byInstance.Values.Where(x => x.Item.Cyber!.RequiresPair).GroupBy(x => x.Item.Id))
        {
            var item = group.First().Item;
            if (item.Cyber!.RequiresItemId is null)
            {
                c.Warn("CYBER_PAIR_CHECK", $"CYBER_PAIR_CHECK:{item.Id}", L.T($"{item.Name} é instalado em par; confira se a ficha tem o par completo.", $"{item.Name} is installed as a pair; check that the sheet has the full pair."), null,
                    item.SourceBook is null ? null : $"{item.SourceBook} p.{item.Page}", "cyberware");
                continue;
            }
            var parents = group.Select(x => x.Entry.ParentInstanceId).Where(p => p is not null).Distinct().Count();
            if (group.Count() % 2 != 0 || parents < 2)
                c.Hard("CYBER_PAIR_MISSING", $"CYBER_PAIR_MISSING:{item.Id}",
                    L.T($"{item.Name} precisa ser instalado em par, um em cada peça base ({group.Count()} instalado(s) em {parents}).", $"{item.Name} must be installed as a pair, one in each foundational piece ({group.Count()} installed in {parents})."),
                    L.T("Instale uma unidade em cada uma de duas peças base (ex.: dois Cybereyes).", "Install one unit in each of two foundational pieces (e.g. two Cybereyes)."), "CP:R p.360 (must be paired)", "cyberware");
        }

        // Same option twice in the same parent, or the same standalone piece twice: benefits don't stack.
        foreach (var dup in byInstance.Values.Where(x => !x.Item.Cyber!.Foundational && !x.Item.Cyber.RequiresPair)
                     .GroupBy(x => (x.Item.Id, x.Entry.ParentInstanceId)).Where(g => g.Count() > 1))
            c.Warn("CYBER_DUPLICATE", $"CYBER_DUPLICATE:{dup.Key.Id}:{dup.Key.ParentInstanceId}",
                L.T($"{dup.First().Item.Name} aparece {dup.Count()} vezes no mesmo local; os benefícios não se somam.", $"{dup.First().Item.Name} appears {dup.Count()} times in the same location; the benefits don't stack."), null,
                rules.Document.Cyberware.StackingNote.Source, "cyberware");

        // Required / forbidden choices made in the generator are part of the sheet.
        foreach (var dup in c.Sheet.CyberwareRules.GroupBy(r => r.ItemId).Where(g => g.Count() > 1))
            c.Hard("CYBER_RULE_DUPLICATE", $"CYBER_RULE_DUPLICATE:{dup.Key}", L.T($"O implante {dup.Key} tem mais de uma regra (obrigatório/proibido).", $"Implant {dup.Key} has more than one rule (required/forbidden)."), null, null, "cyberwareRules");
        foreach (var rule in c.Sheet.CyberwareRules)
        {
            var name = c.Catalog.Find(rule.ItemId)?.Name ?? $"Item {rule.ItemId}";
            var present = byInstance.Values.Any(x => x.Item.Id == rule.ItemId);
            if (rule.State == CyberwareRuleState.Forbidden && present)
                c.Hard("CYBER_FORBIDDEN", $"CYBER_FORBIDDEN:{rule.ItemId}", L.T($"{name} está marcado como proibido, mas está na ficha.", $"{name} is marked as forbidden, but it's on the sheet."), L.T("Remova o implante ou libere-o.", "Remove the implant or allow it."), null, "cyberware");
            if (rule.State == CyberwareRuleState.Required && !present)
                c.Hard("CYBER_REQUIRED_MISSING", $"CYBER_REQUIRED_MISSING:{rule.ItemId}", L.T($"{name} está marcado como obrigatório, mas não está na ficha.", $"{name} is marked as required, but it isn't on the sheet."), L.T("Adicione o implante ou desmarque a obrigatoriedade.", "Add the implant or unmark it as required."), null, "cyberware");
        }

        var budget = c.T.CyberwareBudget;
        if (budget.Value is { } limit && total > limit)
            c.Error("CYBER_BUDGET_EXCEEDED", "CYBER_BUDGET_EXCEEDED",
                L.T($"O cyberware custa {Eb(total)} e ultrapassa o orçamento de {Eb(limit)} em {Eb(total - limit)}.", $"The cyberware costs {Eb(total)} and goes {Eb(total - limit)} over the budget of {Eb(limit)}."),
                L.T("Remova implantes ou troque por versões mais baratas.", "Remove implants or swap them for cheaper versions."), budget.Reference, "cyberware");

        return (lines, new BudgetLine(total, budget.Value, budget.Reference));
    }

    // ------------------------------------------------------------ derived values

    private NpcDerived ComputeDerived(Context c, List<CyberLine> cyberLines)
    {
        int Stat(string k) => c.Sheet.Stats.GetValueOrDefault(k);
        var body = Stat("BODY");

        var frames = c.Sheet.Cyberware.Select(e => c.Catalog.Find(e.ItemId)).Where(i => i?.Armor is { Implanted: true, BodyValue: not null }).ToList();
        var bodyForDerived = frames.Count > 0 ? Math.Max(body, frames.Max(f => f!.Armor!.BodyValue!.Value)) : body;
        var carriedFrames = c.Sheet.Gear.Select(g => c.Catalog.Find(g.ItemId)).Where(i => i?.Armor is { Type: ArmorType.LinearFrame, Implanted: false, BodyValue: not null }).ToList();
        var strengthBody = carriedFrames.Count > 0 ? Math.Max(bodyForDerived, carriedFrames.Max(f => f!.Armor!.BodyValue!.Value)) : bodyForDerived;

        var worn = c.Sheet.Gear.Where(g => g.Worn is not null)
            .Select(g => (g.Worn, Item: c.Catalog.Find(g.ItemId)))
            .Where(x => x.Item?.Armor is { Type: ArmorType.BodyArmor })
            .ToList();
        var cyberSp = cyberLines.Select(l => rules.CyberwareStoppingPower(l.Slug) ?? 0).DefaultIfEmpty(0).Max();
        int Sp(ArmorSlot slot) => Math.Max(cyberSp,
            worn.Where(x => x.Worn == slot).Select(x => x.Item!.Armor!.Sp ?? 0).DefaultIfEmpty(0).Max());

        var penalty = new ArmorPenalty(
            worn.Select(x => x.Item!.Armor!.RefPenalty).DefaultIfEmpty(0).Max(),
            worn.Select(x => x.Item!.Armor!.DexPenalty).DefaultIfEmpty(0).Max(),
            worn.Select(x => x.Item!.Armor!.MovePenalty).DefaultIfEmpty(0).Max());

        var effective = rules.StatKeys.ToDictionary(k => k, k => Math.Max(0, Stat(k) - (k switch
        {
            "REF" => penalty.Ref,
            "DEX" => penalty.Dex,
            "MOVE" => penalty.Move,
            _ => 0,
        })), StringComparer.OrdinalIgnoreCase);

        var hp = _m.HitPoints(bodyForDerived, Stat("WILL"));
        var pieces = cyberLines.Count;
        var perPoint = rules.Document.EmpGuideline.PiecesPerPoint.Value;
        int? suggestedEmp = pieces >= perPoint ? Math.Max(0, Stat("EMP") - pieces / perPoint) : null;

        return new NpcDerived(hp, NpcMechanics.SeriouslyWounded(hp), bodyForDerived, bodyForDerived, strengthBody,
            Sp(ArmorSlot.Body), Sp(ArmorSlot.Head), penalty, effective, pieces, suggestedEmp);
    }

    private void ValidateDerived(Context c, NpcDerived d)
    {
        var hp = c.T.SuggestedHp;
        if (!hp.Value.Contains(d.Hp))
            c.Warn("HP_OUT_OF_RANGE", "HP_OUT_OF_RANGE", L.T($"HP {d.Hp} fora da faixa sugerida de {hp.Value} para {c.T.DisplayName}.", $"HP {d.Hp} outside the suggested range of {hp.Value} for {c.T.DisplayName}."),
                d.Hp < hp.Value.Min ? L.T("Suba BODY ou WILL.", "Raise BODY or WILL.") : L.T("Reduza BODY ou WILL.", "Lower BODY or WILL."), hp.Reference, "stats");

        var sp = c.T.SuggestedSp;
        if (!sp.Value.Contains(d.SpBody))
            c.Warn("SP_OUT_OF_RANGE", "SP_OUT_OF_RANGE", L.T($"SP do corpo {d.SpBody} fora da faixa sugerida de {sp.Value} para {c.T.DisplayName}.", $"Body SP {d.SpBody} outside the suggested range of {sp.Value} for {c.T.DisplayName}."),
                d.SpBody < sp.Value.Min ? L.T("Vista uma armadura mais forte.", "Wear stronger armor.") : L.T("Use uma armadura mais leve.", "Use lighter armor."), sp.Reference, "gear");
        if (d.SpHead == 0 && d.SpBody > 0)
            c.Info("SP_HEAD_EMPTY", "SP_HEAD_EMPTY", L.T("A cabeça está sem armadura.", "The head has no armor."), L.T("Adicione uma peça de armadura na cabeça, se fizer sentido.", "Add a piece of head armor, if it makes sense."), null, "gear");

        if (d.SuggestedEmp is { } emp && emp < c.Sheet.Stats.GetValueOrDefault("EMP"))
            c.Info("EMP_GUIDELINE", "EMP_GUIDELINE",
                L.T($"Com {d.CyberwarePieces} peças de cyberware, a orientação sugere EMP {emp} (opcional).", $"With {d.CyberwarePieces} cyberware pieces, the guideline suggests EMP {emp} (optional)."), null,
                rules.Document.EmpGuideline.PiecesPerPoint.Source, "stats.EMP");
        if (c.T.CyberwareBudget.Unlimited && d.CyberwarePieces > 30)
            c.Info("CYBERPSYCHO", "CYBERPSYCHO", L.T("Mais de 30 peças de cyberware: provavelmente um ciberpsicopata com EMP 0.", "More than 30 cyberware pieces: probably a cyberpsycho with EMP 0."), null, c.T.CyberwareBudget.Source, "cyberware");
    }

    // ------------------------------------------------------------ skills

    private (List<SkillLine>, BudgetLine) ValidateSkills(Context c, NpcDerived d)
    {
        var free = rules.FreeSkillLevels(c.Sheet.NativeLanguage, c.Sheet.Home);
        var keys = c.Sheet.Skills.Keys.Concat(free.Keys).Concat(c.Sheet.KeySkills).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var maxInput = rules.Document.SkillCost.MaxLevelInput.Value;
        var lines = new List<SkillLine>();
        var used = 0;

        foreach (var key in keys)
        {
            var (id, spec) = SkillKey.Parse(key);
            var field = $"skills.{key}";
            if (rules.FindSkill(id) is not { } def)
            {
                c.Hard("SKILL_UNKNOWN", $"SKILL_UNKNOWN:{key}", L.T($"Perícia \"{id}\" não existe na lista do Core.", $"Skill \"{id}\" isn't on the Core list."), null, "CP:R p.82-85", field);
                continue;
            }
            if (def.Specialized && spec is null)
            {
                c.Hard("SKILL_SPECIALIZATION_REQUIRED", $"SKILL_SPECIALIZATION_REQUIRED:{key}", L.T($"{def.Name} exige especificar qual (ex.: idioma, região ou estilo).", $"{def.Name} needs you to specify which (e.g. language, region or form)."), null, $"CP:R p.{def.Page}", field);
                continue;
            }
            if (!def.Specialized && spec is not null)
            {
                c.Hard("SKILL_SPECIALIZATION_INVALID", $"SKILL_SPECIALIZATION_INVALID:{key}", L.T($"{def.Name} não tem especialização.", $"{def.Name} has no specialization."), null, $"CP:R p.{def.Page}", field);
                continue;
            }

            var freeLevel = free.GetValueOrDefault(key);
            var level = c.Sheet.Skills.TryGetValue(key, out var lv) ? lv : freeLevel;
            if (level < 0 || level > maxInput * 2)
            {
                c.Hard("SKILL_INVALID", $"SKILL_INVALID:{key}", L.T($"Nível de {def.Name} inválido ({level}).", $"Invalid {def.Name} level ({level})."), null, null, field);
                continue;
            }
            if (level > c.T.MaxSkill.Value)
                c.Error("SKILL_ABOVE_MAX", $"SKILL_ABOVE_MAX:{key}",
                    L.T($"A perícia {Display(def, spec)} (nível {level}) ultrapassa o nível máximo de {c.T.MaxSkill.Value} permitido para esta categoria.", $"The {Display(def, spec)} skill (level {level}) goes over the maximum level of {c.T.MaxSkill.Value} allowed for this category."),
                    L.T($"Reduza para {c.T.MaxSkill.Value}.", $"Lower it to {c.T.MaxSkill.Value}."), c.T.MaxSkill.Reference, field);
            if (level < freeLevel)
                c.Warn("SKILL_BELOW_FREE", $"SKILL_BELOW_FREE:{key}", L.T($"{Display(def, spec)} está abaixo do nível gratuito {freeLevel} que todo NPC recebe.", $"{Display(def, spec)} is below the free level {freeLevel} every NPC gets."),
                    L.T($"Use pelo menos {freeLevel}; esses níveis não custam pontos.", $"Use at least {freeLevel}; those levels cost no points."), rules.Document.FreeSkills.Source, field);

            var mult = rules.SkillCostMultiplier(def);
            var cost = Math.Max(0, level - freeLevel) * mult;
            used += cost;
            var statValue = d.EffectiveStats.GetValueOrDefault(def.Stat);
            var isKey = c.Sheet.KeySkills.Contains(key, StringComparer.OrdinalIgnoreCase);
            lines.Add(new SkillLine(key, def.Id, def.Name, spec, def.Stat, def.Category, level, freeLevel, cost, statValue + level,
                def.DoubleCost, def.Combat, isKey));
        }

        var budget = c.T.SkillPoints.Value;
        if (used > budget)
            c.Error("SKILL_BUDGET_EXCEEDED", "SKILL_BUDGET_EXCEEDED",
                L.T($"O NPC ultrapassa o orçamento de perícias em {Points(used - budget)}.", $"The NPC goes {Points(used - budget)} over the skill budget."),
                L.T($"Remova {Points(used - budget)} (perícias x2 custam o dobro). Orçamento de {budget} para {c.T.DisplayName}.", $"Remove {Points(used - budget)} (x2 skills cost double). Budget of {budget} for {c.T.DisplayName}."),
                c.T.SkillPoints.Reference, "skills");
        else if (used < budget)
            c.Info("SKILL_BUDGET_UNSPENT", "SKILL_BUDGET_UNSPENT", L.T($"Restam {Points(budget - used)} de perícia para distribuir.", $"{Points(budget - used)} of skill left to spend."), null, c.T.SkillPoints.Reference, "skills");

        foreach (var line in lines.Where(l => l.IsKey))
        {
            var range = line.Combat ? c.T.CombatSkillBase : c.T.NonCombatSkillBase;
            if (!range.Value.Contains(line.Base))
                c.Warn("KEY_SKILL_BASE_OUT_OF_RANGE", $"KEY_SKILL_BASE_OUT_OF_RANGE:{line.Key}",
                    L.T($"Base de {Display(line)} é {line.Base} ({line.Stat} {line.Base - line.Level} + nível {line.Level}); a faixa sugerida para perícias {(line.Combat ? "de combate" : "não combate")} importantes é {range.Value}.", $"{Display(line)} base is {line.Base} ({line.Stat} {line.Base - line.Level} + level {line.Level}); the suggested range for important {(line.Combat ? "combat" : "non-combat")} skills is {range.Value}."),
                    line.Base < range.Value.Min ? L.T("Suba o nível da perícia ou o STAT ligado a ela.", "Raise the skill level or its linked STAT.") : L.T("Reduza o nível da perícia.", "Lower the skill level."),
                    range.Reference, $"skills.{line.Key}");
        }
        if (!lines.Any(l => l.IsKey && l.Combat))
            c.Info("KEY_SKILLS_MISSING", "KEY_SKILLS_MISSING", L.T("Nenhuma perícia de combate marcada como importante; as bases sugeridas não foram conferidas.", "No combat skill marked as important; the suggested bases weren't checked."),
                L.T("Marque a arma principal e Evasion como importantes.", "Mark the main weapon and Evasion as important."), c.T.CombatSkillBase.Reference, "keySkills");

        return (lines, new BudgetLine(used, budget, c.T.SkillPoints.Reference));
    }

    private static string Display(SkillDefinition def, string? spec) => spec is null ? def.Name : $"{def.Name} ({spec})";
    private static string Display(SkillLine l) => l.Specialization is null ? l.Name : $"{l.Name} ({l.Specialization})";

    // ------------------------------------------------------------ weapon guidelines

    private void ValidateWeapons(Context c, NpcDerived d)
    {
        for (var i = 0; i < c.Sheet.Gear.Count; i++)
        {
            var g = c.Sheet.Gear[i];
            if (c.Catalog.Find(g.ItemId) is not { Weapon: { } w } item) continue;
            var quality = g.Quality ?? w.Quality;
            var field = $"gear[{i}]";

            switch (c.T.WeaponQuality.Value)
            {
                case WeaponQualityRule.PoorOnly when quality != WeaponQuality.Poor:
                    c.Warn("WEAPON_QUALITY_GUIDELINE", $"WEAPON_QUALITY_GUIDELINE:{i}",
                        L.T($"{item.Name} é {NpcMechanics.Label(quality)}; a orientação para Mooks é usar armas Poor Quality.", $"{item.Name} is {NpcMechanics.Label(quality)}; the guideline for Mooks is to use Poor Quality weapons."),
                        w.Exotic ? null : L.T("Troque a qualidade para Poor Quality (mais barata).", "Change the quality to Poor Quality (cheaper)."), c.T.WeaponQuality.Reference, field);
                    break;
                case WeaponQualityRule.NoExcellent when quality == WeaponQuality.Excellent:
                    c.Error("WEAPON_EXCELLENT_NOT_ALLOWED", $"WEAPON_EXCELLENT_NOT_ALLOWED:{i}",
                        L.T($"{item.Name} é Excellent Quality, o que o template de {c.T.Label} não permite.", $"{item.Name} is Excellent Quality, which the {c.T.Label} template doesn't allow."),
                        L.T("Use a versão Standard ou registre uma exceção.", "Use the Standard version or record an exception."), c.T.WeaponQuality.Reference, field);
                    break;
            }
            if (c.T.Hardened && c.T.Level != NpcLevel.Mook && quality == WeaponQuality.Poor)
                c.Warn("HARDENED_POOR_QUALITY", $"HARDENED_POOR_QUALITY:{i}", L.T($"{item.Name} é Poor Quality; NPCs Hardened não deveriam ter itens Poor Quality, exceto Mooks.", $"{item.Name} is Poor Quality; Hardened NPCs shouldn't have Poor Quality items, except Mooks."),
                    L.T("Use a versão Standard.", "Use the Standard version."), rules.Document.HardenedGuidance.FirstOrDefault()?.Source, field);
            if (w.MinBody is { } minBody && d.StrengthBody < minBody)
                c.Error("WEAPON_MIN_BODY", $"WEAPON_MIN_BODY:{i}", L.T($"O equipamento selecionado não atende aos requisitos cadastrados: {item.Name} exige BODY {minBody} (NPC tem {d.StrengthBody}).", $"The selected gear doesn't meet its listed requirements: {item.Name} needs BODY {minBody} (the NPC has {d.StrengthBody})."),
                    L.T($"Suba BODY para {minBody} ou escolha outra arma.", $"Raise BODY to {minBody} or pick another weapon."), item.SourceBook is null ? null : $"{item.SourceBook} p.{item.Page}", field);
        }
    }

    // ------------------------------------------------------------ Role

    private void ValidateRole(Context c)
    {
        var guide = c.T.HardenedRoleRank ?? c.T.RoleRank;
        if (c.Sheet.RoleRank > c.T.MaxRoleRank)
            c.Warn("ROLE_RANK_ABOVE_GUIDELINE", "ROLE_RANK_ABOVE_GUIDELINE",
                L.T($"Rank {c.Sheet.RoleRank} acima do sugerido para {c.T.DisplayName} (até {c.T.MaxRoleRank}).", $"Rank {c.Sheet.RoleRank} above the suggested for {c.T.DisplayName} (up to {c.T.MaxRoleRank})."), null, guide.Reference, "roleRank");
        if (c.T.RoleRank.Value.Presence == RolePresence.Has && c.Sheet.RoleRank < c.T.RoleRank.Value.Rank)
            c.Info("ROLE_EXPECTED", "ROLE_EXPECTED",
                L.T($"{c.T.Label} normalmente tem Role Ability Rank {c.T.RoleRank.Value.Rank}.", $"{c.T.Label} usually has Role Ability Rank {c.T.RoleRank.Value.Rank}."), "Defina Role e Rank.", c.T.RoleRank.Reference, "roleRank");
    }

    // ------------------------------------------------------------ exceptions

    private static List<NpcIssue> ApplyExceptions(Context c)
    {
        var exceptions = c.Sheet.Exceptions
            .Where(e => !string.IsNullOrWhiteSpace(e.Key) && !string.IsNullOrWhiteSpace(e.Justification))
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Justification.Trim(), StringComparer.Ordinal);

        return c.Issues
            .Select(i => i is { Severity: NpcIssueSeverity.Error, Overridable: true } && exceptions.TryGetValue(i.Key, out var why)
                ? i with { Overridden = true, Justification = why }
                : i)
            .OrderBy(i => i.Severity)
            .ToList();
    }

    private static string Points(int n) => n == 1 ? "1 ponto" : $"{n} pontos";
    internal static string Eb(int n) => $"{n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}eb";

    private sealed class Context(NpcSheet sheet, NpcTemplate t, NpcCatalog catalog)
    {
        public NpcSheet Sheet { get; } = sheet;
        public NpcTemplate T { get; } = t;
        public NpcCatalog Catalog { get; } = catalog;
        public List<NpcIssue> Issues { get; } = [];

        /// <summary>Guideline violation: blocks "Valid" but the GM can accept it with a justified exception.</summary>
        public void Error(string code, string key, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new NpcIssue(code, key, NpcIssueSeverity.Error, message, suggestion, source, true, field));

        /// <summary>Structural or mechanical impossibility: never accepted as an exception.</summary>
        public void Hard(string code, string key, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new NpcIssue(code, key, NpcIssueSeverity.Error, message, suggestion, source, false, field));

        public void Warn(string code, string key, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new NpcIssue(code, key, NpcIssueSeverity.Warning, message, suggestion, source, false, field));

        public void Info(string code, string key, string message, string? suggestion, string? source, string? field) =>
            Issues.Add(new NpcIssue(code, key, NpcIssueSeverity.Info, message, suggestion, source, false, field));
    }
}
