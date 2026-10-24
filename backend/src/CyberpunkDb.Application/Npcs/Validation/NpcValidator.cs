using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

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
            c.Warn("NAME_MISSING", "NAME_MISSING", "O NPC está sem nome.", "Dê um nome ou apelido antes de salvar.", null, "name");
        if (c.Sheet.Role is { } role && !rules.Document.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            c.Hard("ROLE_UNKNOWN", "ROLE_UNKNOWN", $"Role \"{role}\" não existe.", $"Use um destes: {string.Join(", ", rules.Document.Roles)}.", null, "role");
        if (c.Sheet.GangId is { } gang && rules.FindGang(gang) is null)
            c.Info("GANG_UNKNOWN", "GANG_UNKNOWN", $"A gangue \"{gang}\" não está na lista de gangues.", "Escolha outra gangue ou deixe sem gangue.", null, "gangId");
        if (c.Sheet.RoleRank is < 0 or > 10)
            c.Hard("ROLE_RANK_INVALID", "ROLE_RANK_INVALID", "O Rank da Role Ability deve estar entre 0 e 10.", null, null, "roleRank");
        else if (c.Sheet.RoleRank > 0 && c.Sheet.Role is null)
            c.Hard("ROLE_RANK_WITHOUT_ROLE", "ROLE_RANK_WITHOUT_ROLE", "Há Rank de Role Ability sem Role escolhida.", "Escolha a Role ou zere o Rank.", null, "role");
    }

    private BudgetLine ValidateStats(Context c)
    {
        var s = rules.Document.Stats;
        foreach (var key in c.Sheet.Stats.Keys.Where(k => !rules.StatKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
            c.Hard("STAT_UNKNOWN", $"STAT_UNKNOWN:{key}", $"STAT \"{key}\" não faz parte do orçamento de NPC.",
                key.Equals("LUCK", StringComparison.OrdinalIgnoreCase) ? "LUCK fica no campo próprio, fora do orçamento." : null, s.Luck.Source, $"stats.{key}");

        var used = 0;
        foreach (var key in rules.StatKeys)
        {
            if (!c.Sheet.Stats.TryGetValue(key, out var v))
            {
                c.Hard("STAT_MISSING", $"STAT_MISSING:{key}", $"Informe o valor de {key}.", null, null, $"stats.{key}");
                continue;
            }
            if (v < 0 || v > s.InputMax.Value)
            {
                c.Hard("STAT_INVALID", $"STAT_INVALID:{key}", $"{key} {v} está fora do intervalo aceito (0 a {s.InputMax.Value}).", null, null, $"stats.{key}");
                continue;
            }
            used += v;
            var min = key == "EMP" ? s.EmpMin.Value : s.Min.Value;
            if (v < min)
                c.Error("STAT_BELOW_MIN", $"STAT_BELOW_MIN:{key}", $"{key} {v} está abaixo do mínimo de {min}.",
                    $"Aumente {key} para {min}.", s.Min.Source, $"stats.{key}");
            if (v > c.T.MaxStat.Value)
                c.Error("STAT_ABOVE_MAX", $"STAT_ABOVE_MAX:{key}", $"{key} {v} ultrapassa o máximo de {c.T.MaxStat.Value} para {c.T.DisplayName}.",
                    $"Reduza {key} para {c.T.MaxStat.Value}" + (key == "BODY" ? " ou use um Implanted Linear Frame." : "."), c.T.MaxStat.Reference, $"stats.{key}");
        }

        var budget = c.T.StatPoints.Value;
        if (used > budget)
            c.Error("STAT_BUDGET_EXCEEDED", "STAT_BUDGET_EXCEEDED",
                $"O NPC ultrapassa o orçamento de STATs em {Points(used - budget)}.",
                $"Remova {Points(used - budget)} distribuídos entre os STATs (orçamento de {budget} para {c.T.DisplayName}).",
                c.T.StatPoints.Reference, "stats");
        else if (used < budget)
            c.Info("STAT_BUDGET_UNSPENT", "STAT_BUDGET_UNSPENT", $"Restam {Points(budget - used)} de STAT para distribuir.", null, c.T.StatPoints.Reference, "stats");

        if (c.Sheet.Luck is > 0)
            c.Info("LUCK_PRESENT", "LUCK_PRESENT", $"LUCK {c.Sheet.Luck} fica fora do orçamento de STATs. NPCs raramente têm LUCK.", null, s.Luck.Source, "luck");

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
                c.Hard("ITEM_NOT_FOUND", $"ITEM_NOT_FOUND:gear:{i}", $"Item {g.ItemId} não existe no catálogo.", "Remova o item da ficha.", null, field);
                continue;
            }
            if (item.IsCyberwareBudget)
            {
                c.Hard("ITEM_WRONG_LIST", $"ITEM_WRONG_LIST:gear:{i}", $"{item.Name} é cyberware e entra na seção de cyberware.", "Mova o item para Cyberware.", null, field);
                continue;
            }
            if (g.Quantity is < 1 or > 999)
                c.Hard("ITEM_QUANTITY_INVALID", $"ITEM_QUANTITY_INVALID:{i}", $"Quantidade de {item.Name} deve estar entre 1 e 999.", null, null, field);

            var (unit, quality, qualityError) = _m.GearUnitPrice(item, g.Quality);
            if (g.Quality is not null && item.Weapon is null)
                c.Hard("QUALITY_NOT_WEAPON", $"QUALITY_NOT_WEAPON:{i}", $"Qualidade só se aplica a armas ({item.Name}).", null, null, field);
            if (qualityError is not null)
                c.Hard("WEAPON_QUALITY_UNAVAILABLE", $"WEAPON_QUALITY_UNAVAILABLE:{i}", qualityError, "Mantenha a qualidade cadastrada ou escolha outra arma.",
                    rules.Document.WeaponQualityPricing.Source, field);
            if (unit is null && qualityError is null)
                c.Warn("ITEM_PRICE_UNKNOWN", $"ITEM_PRICE_UNKNOWN:{i}", $"{item.Name} não tem preço fixo no catálogo; não entra no orçamento.", "Confira o preço com o Mestre.", null, field);

            if (g.Worn is { } slot)
            {
                if (item.Armor is not { Type: ArmorType.BodyArmor })
                    c.Hard("ARMOR_NOT_WEARABLE", $"ARMOR_NOT_WEARABLE:{i}", $"{item.Name} não é uma armadura que se veste no corpo ou na cabeça.", "Deixe o item apenas como carregado.", null, field);
                else if (g.Quantity != 1)
                    c.Hard("ARMOR_WORN_QUANTITY", $"ARMOR_WORN_QUANTITY:{i}", $"Cada armadura vestida corresponde a uma peça ({item.Name}).", "Use quantidade 1 e adicione outra linha para o outro local.", null, field);
            }

            var lineTotal = (unit ?? 0) * Math.Max(0, g.Quantity);
            total += lineTotal;
            lines.Add(new GearLine(i, item.Id, item.Name, item.Slug, item.Category, g.Quantity, item.Weapon is null ? null : quality,
                unit, lineTotal, g.Worn, GearSummary(item), g.Locked));
        }

        foreach (var group in lines.Where(l => l.Worn is not null).GroupBy(l => l.Worn!.Value).Where(g => g.Count() > 1))
            c.Error("ARMOR_SLOT_CONFLICT", $"ARMOR_SLOT_CONFLICT:{group.Key}",
                $"Há {group.Count()} armaduras vestidas {(group.Key == ArmorSlot.Body ? "no corpo" : "na cabeça")}: {string.Join(", ", group.Select(l => l.Name))}.",
                "Vista só uma por local; as outras ficam carregadas.", rules.Document.Armor.OneArmorPerLocation.Reference, "gear");

        var budget = c.T.GearBudget.Value;
        if (total > budget)
            c.Error("GEAR_BUDGET_EXCEEDED", "GEAR_BUDGET_EXCEEDED",
                $"O equipamento custa {Eb(total)} e ultrapassa o orçamento de {Eb(budget)} em {Eb(total - budget)}.",
                "Troque por itens mais baratos, use Poor Quality ou remova itens.", c.T.GearBudget.Reference, "gear");
        return (lines, new BudgetLine(total, budget, c.T.GearBudget.Reference));
    }

    private static string? GearSummary(CatalogItem item) => item switch
    {
        { Weapon: { } w } => $"{w.Damage} · ROF {w.Rof}" + (w.Exotic ? " · exótica" : ""),
        { Armor: { Sp: { } sp } a } => $"SP {sp}" + (a.RefPenalty > 0 ? $" · -{a.RefPenalty} REF/DEX/MOVE" : ""),
        { Armor: { BodyValue: { } b } } => $"BODY {b}",
        { Ammo: { } a } => a.PerPack is { } n ? $"pacote com {n}" : null,
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
                c.Hard("CYBER_INSTANCE_INVALID", $"CYBER_INSTANCE_INVALID:{i}", "Cada implante precisa de um identificador único.", null, null, field);
                continue;
            }
            if (c.Catalog.Find(e.ItemId) is not { } item)
            {
                c.Hard("ITEM_NOT_FOUND", $"ITEM_NOT_FOUND:cyber:{e.InstanceId}", $"Item {e.ItemId} não existe no catálogo.", "Remova o implante da ficha.", null, field);
                continue;
            }
            if (!item.IsCyberwareBudget)
            {
                c.Hard("ITEM_WRONG_LIST", $"ITEM_WRONG_LIST:cyber:{e.InstanceId}", $"{item.Name} não é cyberware.", "Mova o item para Equipamentos.", null, field);
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
                    c.Hard("CYBER_PARENT_MISSING", $"CYBER_PARENT_MISSING:{id}", $"{item.Name} aponta para uma peça base que não está na ficha.",
                        required is null ? "Escolha onde ele está instalado." : $"Adicione {required.Name} e instale {item.Name} nele.", rules.Document.Cyberware.OptionSlotDefault.Source, field);
                else if (parent.Item.Cyber is not { Foundational: true })
                    c.Hard("CYBER_PARENT_INVALID", $"CYBER_PARENT_INVALID:{id}", $"{parent.Item.Name} não é uma peça base com Option Slots.", null, rules.Document.Cyberware.OptionSlotDefault.Source, field);
                else if (required is not null && !NpcMechanics.ParentCompatible(parent.Item, required))
                    c.Hard("CYBER_PARENT_WRONG", $"CYBER_PARENT_WRONG:{id}", $"{item.Name} precisa ser instalado em {required.Name}, não em {parent.Item.Name}.", null, null, field);
                else
                    usedInParent[pid] = usedInParent.GetValueOrDefault(pid) + slots;
            }
            else if (required is not null)
            {
                c.Hard("CYBER_PARENT_MISSING", $"CYBER_PARENT_MISSING:{id}", $"{item.Name} exige {required.Name} como peça base.",
                    $"Adicione {required.Name} e instale {item.Name} nele.", rules.Document.Cyberware.OptionSlotDefault.Source, field);
            }
            else if (_m.PoolFor(item) is { } p)
            {
                pool = p.Key;
                usedInPool[p.Key] = usedInPool.GetValueOrDefault(p.Key) + slots;
            }

            if (assumed && (e.ParentInstanceId is not null || pool is not null) && assumedReported.Add(item.Id))
                c.Info("CYBER_SLOTS_ASSUMED", $"CYBER_SLOTS_ASSUMED:{item.Id}", $"{item.Name} não informa quantos slots ocupa; contado como 1.", null,
                    rules.Document.Cyberware.OptionSlotDefault.Source, field);

            var freeText = _m.FreeTextRequirement(item, c.Catalog);
            if (freeText is not null)
                c.Warn("CYBER_REQUIREMENT_TEXT", $"CYBER_REQUIREMENT_TEXT:{id}", $"{item.Name} tem pré-requisito que o sistema não verifica: \"{freeText}\".",
                    "Confira manualmente com o livro.", item.SourceBook is null ? null : $"{item.SourceBook}{(item.Page is { } pg ? $" p.{pg}" : "")}", field);

            if (item.Price is null)
                c.Warn("ITEM_PRICE_UNKNOWN", $"ITEM_PRICE_UNKNOWN:cyber:{id}", $"{item.Name} não tem preço fixo no catálogo; não entra no orçamento.", null, null, field);
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
                        $"{line.Name} tem {provided} Option Slots, mas as opções instaladas ocupam {used}.",
                        $"Remova opções ou instale uma segunda peça base.", rules.Document.Cyberware.OptionSlotDefault.Source, "cyberware");
            }
        }

        foreach (var pool in rules.Document.Cyberware.Pools)
        {
            var used = usedInPool.GetValueOrDefault(pool.Key);
            if (used > pool.Slots)
                c.Hard("CYBER_POOL_EXCEEDED", $"CYBER_POOL_EXCEEDED:{pool.Key}",
                    $"{pool.Label} comporta {pool.Slots} slots; a ficha usa {used}.", "Remova implantes dessa categoria.", pool.Source, "cyberware");
        }

        // Body capacity (two eyes, one Cyberaudio Suite...).
        foreach (var rule in rules.Document.Cyberware.Capacity)
        {
            var matching = byInstance.Values.Where(x => NpcMechanics.MatchesCapacity(rule, x.Item)).ToList();
            if (matching.Count > rule.Max)
                c.Hard("CYBER_CAPACITY_EXCEEDED", $"CYBER_CAPACITY_EXCEEDED:{rule.Label}",
                    $"{rule.Label}: no máximo {rule.Max}, a ficha tem {matching.Count} ({string.Join(", ", matching.Select(x => x.Item.Name))}).",
                    "Remova o excedente.", rule.Reference, "cyberware");
        }

        foreach (var group in rules.Document.Cyberware.ExclusiveGroups)
        {
            var matching = byInstance.Values.Where(x => group.Slugs.Contains(x.Item.Slug, StringComparer.OrdinalIgnoreCase)).ToList();
            if (matching.Count > group.Max)
                c.Hard("CYBER_INCOMPATIBLE", $"CYBER_INCOMPATIBLE:{group.Label}",
                    $"A combinação de implantes selecionada possui incompatibilidades: só {group.Max} {group.Label} por vez ({string.Join(", ", matching.Select(x => x.Item.Name))}).",
                    $"Mantenha apenas um {group.Label}.", group.Source, "cyberware");
        }

        // Paired options: one in each of two different foundational pieces.
        foreach (var group in byInstance.Values.Where(x => x.Item.Cyber!.RequiresPair).GroupBy(x => x.Item.Id))
        {
            var item = group.First().Item;
            if (item.Cyber!.RequiresItemId is null)
            {
                c.Warn("CYBER_PAIR_CHECK", $"CYBER_PAIR_CHECK:{item.Id}", $"{item.Name} é instalado em par; confira se a ficha tem o par completo.", null,
                    item.SourceBook is null ? null : $"{item.SourceBook} p.{item.Page}", "cyberware");
                continue;
            }
            var parents = group.Select(x => x.Entry.ParentInstanceId).Where(p => p is not null).Distinct().Count();
            if (group.Count() % 2 != 0 || parents < 2)
                c.Hard("CYBER_PAIR_MISSING", $"CYBER_PAIR_MISSING:{item.Id}",
                    $"{item.Name} precisa ser instalado em par, um em cada peça base ({group.Count()} instalado(s) em {parents}).",
                    "Instale uma unidade em cada uma de duas peças base (ex.: dois Cybereyes).", "CP:R p.360 (must be paired)", "cyberware");
        }

        // Same option twice in the same parent, or the same standalone piece twice: benefits don't stack.
        foreach (var dup in byInstance.Values.Where(x => !x.Item.Cyber!.Foundational && !x.Item.Cyber.RequiresPair)
                     .GroupBy(x => (x.Item.Id, x.Entry.ParentInstanceId)).Where(g => g.Count() > 1))
            c.Warn("CYBER_DUPLICATE", $"CYBER_DUPLICATE:{dup.Key.Id}:{dup.Key.ParentInstanceId}",
                $"{dup.First().Item.Name} aparece {dup.Count()} vezes no mesmo local; os benefícios não se somam.", null,
                rules.Document.Cyberware.StackingNote.Source, "cyberware");

        // Required / forbidden choices made in the generator are part of the sheet.
        foreach (var dup in c.Sheet.CyberwareRules.GroupBy(r => r.ItemId).Where(g => g.Count() > 1))
            c.Hard("CYBER_RULE_DUPLICATE", $"CYBER_RULE_DUPLICATE:{dup.Key}", $"O implante {dup.Key} tem mais de uma regra (obrigatório/proibido).", null, null, "cyberwareRules");
        foreach (var rule in c.Sheet.CyberwareRules)
        {
            var name = c.Catalog.Find(rule.ItemId)?.Name ?? $"Item {rule.ItemId}";
            var present = byInstance.Values.Any(x => x.Item.Id == rule.ItemId);
            if (rule.State == CyberwareRuleState.Forbidden && present)
                c.Hard("CYBER_FORBIDDEN", $"CYBER_FORBIDDEN:{rule.ItemId}", $"{name} está marcado como proibido, mas está na ficha.", "Remova o implante ou libere-o.", null, "cyberware");
            if (rule.State == CyberwareRuleState.Required && !present)
                c.Hard("CYBER_REQUIRED_MISSING", $"CYBER_REQUIRED_MISSING:{rule.ItemId}", $"{name} está marcado como obrigatório, mas não está na ficha.", "Adicione o implante ou desmarque a obrigatoriedade.", null, "cyberware");
        }

        var budget = c.T.CyberwareBudget;
        if (budget.Value is { } limit && total > limit)
            c.Error("CYBER_BUDGET_EXCEEDED", "CYBER_BUDGET_EXCEEDED",
                $"O cyberware custa {Eb(total)} e ultrapassa o orçamento de {Eb(limit)} em {Eb(total - limit)}.",
                "Remova implantes ou troque por versões mais baratas.", budget.Reference, "cyberware");

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
            c.Warn("HP_OUT_OF_RANGE", "HP_OUT_OF_RANGE", $"HP {d.Hp} fora da faixa sugerida de {hp.Value} para {c.T.DisplayName}.",
                d.Hp < hp.Value.Min ? "Suba BODY ou WILL." : "Reduza BODY ou WILL.", hp.Reference, "stats");

        var sp = c.T.SuggestedSp;
        if (!sp.Value.Contains(d.SpBody))
            c.Warn("SP_OUT_OF_RANGE", "SP_OUT_OF_RANGE", $"SP do corpo {d.SpBody} fora da faixa sugerida de {sp.Value} para {c.T.DisplayName}.",
                d.SpBody < sp.Value.Min ? "Vista uma armadura mais forte." : "Use uma armadura mais leve.", sp.Reference, "gear");
        if (d.SpHead == 0 && d.SpBody > 0)
            c.Info("SP_HEAD_EMPTY", "SP_HEAD_EMPTY", "A cabeça está sem armadura.", "Adicione uma peça de armadura na cabeça, se fizer sentido.", null, "gear");

        if (d.SuggestedEmp is { } emp && emp < c.Sheet.Stats.GetValueOrDefault("EMP"))
            c.Info("EMP_GUIDELINE", "EMP_GUIDELINE",
                $"Com {d.CyberwarePieces} peças de cyberware, a orientação sugere EMP {emp} (opcional).", null,
                rules.Document.EmpGuideline.PiecesPerPoint.Source, "stats.EMP");
        if (c.T.CyberwareBudget.Unlimited && d.CyberwarePieces > 30)
            c.Info("CYBERPSYCHO", "CYBERPSYCHO", "Mais de 30 peças de cyberware: provavelmente um ciberpsicopata com EMP 0.", null, c.T.CyberwareBudget.Source, "cyberware");
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
                c.Hard("SKILL_UNKNOWN", $"SKILL_UNKNOWN:{key}", $"Perícia \"{id}\" não existe na lista do Core.", null, "CP:R p.82-85", field);
                continue;
            }
            if (def.Specialized && spec is null)
            {
                c.Hard("SKILL_SPECIALIZATION_REQUIRED", $"SKILL_SPECIALIZATION_REQUIRED:{key}", $"{def.Name} exige especificar qual (ex.: idioma, região ou estilo).", null, $"CP:R p.{def.Page}", field);
                continue;
            }
            if (!def.Specialized && spec is not null)
            {
                c.Hard("SKILL_SPECIALIZATION_INVALID", $"SKILL_SPECIALIZATION_INVALID:{key}", $"{def.Name} não tem especialização.", null, $"CP:R p.{def.Page}", field);
                continue;
            }

            var freeLevel = free.GetValueOrDefault(key);
            var level = c.Sheet.Skills.TryGetValue(key, out var lv) ? lv : freeLevel;
            if (level < 0 || level > maxInput * 2)
            {
                c.Hard("SKILL_INVALID", $"SKILL_INVALID:{key}", $"Nível de {def.Name} inválido ({level}).", null, null, field);
                continue;
            }
            if (level > c.T.MaxSkill.Value)
                c.Error("SKILL_ABOVE_MAX", $"SKILL_ABOVE_MAX:{key}",
                    $"A perícia {Display(def, spec)} (nível {level}) ultrapassa o nível máximo de {c.T.MaxSkill.Value} permitido para esta categoria.",
                    $"Reduza para {c.T.MaxSkill.Value}.", c.T.MaxSkill.Reference, field);
            if (level < freeLevel)
                c.Warn("SKILL_BELOW_FREE", $"SKILL_BELOW_FREE:{key}", $"{Display(def, spec)} está abaixo do nível gratuito {freeLevel} que todo NPC recebe.",
                    $"Use pelo menos {freeLevel}; esses níveis não custam pontos.", rules.Document.FreeSkills.Source, field);

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
                $"O NPC ultrapassa o orçamento de perícias em {Points(used - budget)}.",
                $"Remova {Points(used - budget)} (perícias x2 custam o dobro). Orçamento de {budget} para {c.T.DisplayName}.",
                c.T.SkillPoints.Reference, "skills");
        else if (used < budget)
            c.Info("SKILL_BUDGET_UNSPENT", "SKILL_BUDGET_UNSPENT", $"Restam {Points(budget - used)} de perícia para distribuir.", null, c.T.SkillPoints.Reference, "skills");

        foreach (var line in lines.Where(l => l.IsKey))
        {
            var range = line.Combat ? c.T.CombatSkillBase : c.T.NonCombatSkillBase;
            if (!range.Value.Contains(line.Base))
                c.Warn("KEY_SKILL_BASE_OUT_OF_RANGE", $"KEY_SKILL_BASE_OUT_OF_RANGE:{line.Key}",
                    $"Base de {Display(line)} é {line.Base} ({line.Stat} {line.Base - line.Level} + nível {line.Level}); a faixa sugerida para perícias {(line.Combat ? "de combate" : "não combate")} importantes é {range.Value}.",
                    line.Base < range.Value.Min ? "Suba o nível da perícia ou o STAT ligado a ela." : "Reduza o nível da perícia.",
                    range.Reference, $"skills.{line.Key}");
        }
        if (!lines.Any(l => l.IsKey && l.Combat))
            c.Info("KEY_SKILLS_MISSING", "KEY_SKILLS_MISSING", "Nenhuma perícia de combate marcada como importante; as bases sugeridas não foram conferidas.",
                "Marque a arma principal e Evasion como importantes.", c.T.CombatSkillBase.Reference, "keySkills");

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
                        $"{item.Name} é {NpcMechanics.Label(quality)}; a orientação para Mooks é usar armas Poor Quality.",
                        w.Exotic ? null : "Troque a qualidade para Poor Quality (mais barata).", c.T.WeaponQuality.Reference, field);
                    break;
                case WeaponQualityRule.NoExcellent when quality == WeaponQuality.Excellent:
                    c.Error("WEAPON_EXCELLENT_NOT_ALLOWED", $"WEAPON_EXCELLENT_NOT_ALLOWED:{i}",
                        $"{item.Name} é Excellent Quality, o que o template de {c.T.Label} não permite.",
                        "Use a versão Standard ou registre uma exceção.", c.T.WeaponQuality.Reference, field);
                    break;
            }
            if (c.T.Hardened && c.T.Level != NpcLevel.Mook && quality == WeaponQuality.Poor)
                c.Warn("HARDENED_POOR_QUALITY", $"HARDENED_POOR_QUALITY:{i}", $"{item.Name} é Poor Quality; NPCs Hardened não deveriam ter itens Poor Quality, exceto Mooks.",
                    "Use a versão Standard.", rules.Document.HardenedGuidance.FirstOrDefault()?.Source, field);
            if (w.MinBody is { } minBody && d.StrengthBody < minBody)
                c.Error("WEAPON_MIN_BODY", $"WEAPON_MIN_BODY:{i}", $"O equipamento selecionado não atende aos requisitos cadastrados: {item.Name} exige BODY {minBody} (NPC tem {d.StrengthBody}).",
                    $"Suba BODY para {minBody} ou escolha outra arma.", item.SourceBook is null ? null : $"{item.SourceBook} p.{item.Page}", field);
        }
    }

    // ------------------------------------------------------------ Role

    private void ValidateRole(Context c)
    {
        var guide = c.T.HardenedRoleRank ?? c.T.RoleRank;
        if (c.Sheet.RoleRank > c.T.MaxRoleRank)
            c.Warn("ROLE_RANK_ABOVE_GUIDELINE", "ROLE_RANK_ABOVE_GUIDELINE",
                $"Rank {c.Sheet.RoleRank} acima do sugerido para {c.T.DisplayName} (até {c.T.MaxRoleRank}).", null, guide.Reference, "roleRank");
        if (c.T.RoleRank.Value.Presence == RolePresence.Has && c.Sheet.RoleRank < c.T.RoleRank.Value.Rank)
            c.Info("ROLE_EXPECTED", "ROLE_EXPECTED",
                $"{c.T.Label} normalmente tem Role Ability Rank {c.T.RoleRank.Value.Rank}.", "Defina Role e Rank.", c.T.RoleRank.Reference, "roleRank");
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
