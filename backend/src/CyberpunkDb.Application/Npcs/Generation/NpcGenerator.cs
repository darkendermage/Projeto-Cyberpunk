using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Npcs.Generation;

/// <summary>
/// Input of the generator. In automatic mode only <see cref="Level"/> is needed. In semi-automatic mode
/// <see cref="Base"/> carries the user's values; everything locked there (and the name/concept) is kept.
/// </summary>
public sealed record NpcGenerationRequest
{
    public NpcLevel Level { get; init; }
    public bool Hardened { get; init; }
    public string? ProfileId { get; init; }
    /// <summary>Gang laid over the profile (<see cref="GangOverlay"/>): turf, language, handles and favored gear.</summary>
    public string? GangId { get; init; }
    public int? Seed { get; init; }
    public NpcSheet? Base { get; init; }
    public List<NpcCyberwareRule> CyberwareRules { get; init; } = [];
    /// <summary>Lowers EMP by 1 per 5 pieces of cyberware (DGD p.152, optional guideline).</summary>
    public bool ApplyEmpGuideline { get; init; }
}

public sealed record GenerationConflict(string Code, string Message, string? Suggestion, string? Source);

public sealed record NpcGenerationResult(
    bool Success,
    NpcSheet? Sheet,
    NpcValidationReport? Report,
    int Seed,
    int Attempts,
    string? ProfileId,
    IReadOnlyList<GenerationConflict> Conflicts);

public sealed class NpcGenerator(NpcRuleBook rules, NpcValidator validator)
{
    private readonly NpcMechanics _m = new(rules);

    public NpcGenerationResult Generate(NpcGenerationRequest request, NpcCatalog catalog)
    {
        var t = rules.Template(request.Level, request.Hardened);
        var baseSheet = (request.Base ?? new NpcSheet()).Normalize();
        var cyberRules = MergeRules(request.CyberwareRules, baseSheet.CyberwareRules);
        var seed = request.Seed ?? Random.Shared.Next(1, int.MaxValue);

        if (request.ProfileId is not null && rules.FindProfile(request.ProfileId) is null)
            return Fail(seed, 0, null, [new("PROFILE_UNKNOWN", L.T($"Perfil \"{request.ProfileId}\" não existe.", $"Profile \"{request.ProfileId}\" doesn't exist."), L.T("Escolha um perfil da lista ou deixe em branco.", "Pick a profile from the list or leave it blank."), null)]);
        var gang = rules.FindGang(request.GangId);
        if (request.GangId is not null && gang is null)
            return Fail(seed, 0, request.ProfileId, [new("GANG_UNKNOWN", L.T($"Gangue \"{request.GangId}\" não existe.", $"Gang \"{request.GangId}\" doesn't exist."), L.T("Escolha uma gangue da lista ou deixe em branco.", "Pick a gang from the list or leave it blank."), null)]);

        var conflicts = Precheck(t, baseSheet, cyberRules, catalog);
        if (conflicts.Count > 0) return Fail(seed, 0, request.ProfileId, conflicts);

        var profile = rules.FindProfile(request.ProfileId) ?? RandomProfile(gang, new Random(seed));
        NpcValidationReport? last = null;
        var maxAttempts = rules.Document.Generator.MaxAttempts.Value;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var sheet = BuildAttempt(request with { Base = baseSheet, CyberwareRules = cyberRules }, profile, catalog, unchecked(seed + attempt * 7919));
            var report = validator.Validate(sheet, catalog);
            if (report.IsValid) return new NpcGenerationResult(true, sheet, report, seed, attempt + 1, profile.Id, []);
            last = report;
        }

        var reasons = last!.Issues.Where(i => i.Severity == NpcIssueSeverity.Error && !i.Overridden)
            .Select(i => new GenerationConflict(i.Code, i.Message, i.Suggestion, i.Source))
            .DistinctBy(c => c.Message)
            .Prepend(new GenerationConflict("NO_VALID_COMBINATION",
                L.T($"Nenhuma combinação válida foi encontrada em {maxAttempts} tentativas.", $"No valid combination was found in {maxAttempts} attempts."),
                L.T("Libere campos travados, reduza obrigatórios ou mude o perfil.", "Unlock fields, reduce required items or change the profile."), null))
            .ToList();
        return Fail(seed, maxAttempts, profile.Id, reasons);
    }

    /// <summary>
    /// One generation attempt, before validation. Public so tests can check that no intermediate attempt
    /// breaks a budget or a limit.
    /// </summary>
    public NpcSheet BuildAttempt(NpcGenerationRequest request, NpcProfile profile, NpcCatalog catalog, int seed)
    {
        var rng = new Random(seed);
        var t = rules.Template(request.Level, request.Hardened);
        var b = (request.Base ?? new NpcSheet()).Normalize();
        var g = rules.Document.Generator;
        var locks = b.Locks;

        var gang = rules.FindGang(request.GangId);
        var bias = gang?.Bias.Value ?? new GangBias();
        if (gang is not null) profile = GangOverlay.Apply(profile, gang);

        var nativeLanguage = locks.Identity ? b.NativeLanguage
            : gang?.Languages.Value is { Count: > 0 } langs ? Weighted.Pick(langs.ToList(), kv => kv.Value, rng).Key
            : g.Languages[rng.Next(g.Languages.Length)];
        var turf = gang?.Territory.Value is { Length: > 0 } territory ? territory : g.Homes;
        var home = locks.Identity ? b.Home : turf[rng.Next(turf.Length)];
        var stats = DistributeStats(t, profile, b, rng);

        // Cyberware first (it may provide SP), then gear (armor penalties), then skills (bases depend on both).
        var cyber = new CyberwarePlanner(_m, catalog)
            .WithForbidden(request.CyberwareRules.Where(r => r.State == CyberwareRuleState.Forbidden).Select(r => r.ItemId));
        cyber.AddLocked(b.Cyberware.Where(e => e.Locked));
        foreach (var rule in request.CyberwareRules.Where(r => r.State == CyberwareRuleState.Required))
            if (!cyber.Contains(rule.ItemId) && catalog.Find(rule.ItemId) is { Cyber: not null } item)
                cyber.TryInstall(item, t.CyberwareBudget.Value);
        // The gang's taboos only limit what the generator picks on its own; required implants are already in.
        cyber.WithForbidden(bias.ForbiddenCyberware.Select(slug => catalog.Find(slug)?.Id).OfType<int>());
        var cyberLimit = t.CyberwareBudget.Value ?? g.UnlimitedCyberwareReference.Value;
        var spend = bias.CyberwareSpendRange ?? g.CyberwareSpendRange.Value;
        var target = bias.AllCyberwareForbidden ? 0 : (int)(cyberLimit * Weighted.Between(rng, spend.Min, spend.Max));
        cyber.FillOptional(profile, t.CyberwareBudget.Value, target, rng);

        var frames = cyber.Entries.Select(e => catalog.Find(e.ItemId)).Where(i => i?.Armor is { Implanted: true, BodyValue: not null });
        var strengthBody = frames.Select(f => f!.Armor!.BodyValue!.Value).Append(stats["BODY"]).Max();
        var cyberSp = cyber.Entries.Select(e => catalog.Find(e.ItemId)?.Slug).Select(s => s is null ? 0 : rules.CyberwareStoppingPower(s) ?? 0).DefaultIfEmpty(0).Max();

        var gear = new EquipmentPlanner(_m, catalog, t, rng);
        gear.AddLocked(b.Gear.Where(e => e.Locked));
        gear.Fill(profile, strengthBody, cyberSp);

        if (request.ApplyEmpGuideline && !locks.Stats.Contains("EMP", StringComparer.OrdinalIgnoreCase))
        {
            var drop = cyber.Entries.Count / rules.Document.EmpGuideline.PiecesPerPoint.Value;
            stats["EMP"] = Math.Max(rules.Document.Stats.EmpMin.Value, stats["EMP"] - drop);
        }

        var penalties = ArmorPenalties(gear.Entries, catalog);
        var primarySkill = gear.Entries.Select(e => catalog.Find(e.ItemId)?.Weapon).FirstOrDefault(w => w is not null)?.Skill;
        var (skills, keySkills) = DistributeSkills(t, profile, b, stats, penalties, nativeLanguage, home, primarySkill, gear.Entries, catalog, rng);

        var (role, rank) = locks.Identity ? (b.Role, b.RoleRank) : PickRole(t, profile, rng);

        return new NpcSheet
        {
            Name = b.Name.Length > 0 ? b.Name : PickHandle(gang, rng),
            Concept = b.Concept ?? (gang is null ? L.D(profile.Label) : $"{L.D(gang.Label)} · {L.D(profile.Label)}"),
            Level = request.Level,
            Hardened = request.Hardened,
            ProfileId = profile.Id,
            GangId = gang?.Id,
            Role = role,
            RoleRank = rank,
            NativeLanguage = nativeLanguage,
            Home = home,
            Stats = stats,
            Luck = b.Luck,
            Skills = skills,
            KeySkills = keySkills,
            Gear = gear.Entries.ToList(),
            Cyberware = cyber.Entries.ToList(),
            CyberwareRules = request.CyberwareRules,
            Exceptions = b.Exceptions,
            Locks = b.Locks,
            Notes = b.Notes,
        }.Normalize();
    }

    // ------------------------------------------------------------ feasibility

    /// <summary>Checks the user's fixed choices against the template before generating anything.</summary>
    public List<GenerationConflict> Precheck(NpcTemplate t, NpcSheet b, List<NpcCyberwareRule> cyberRules, NpcCatalog catalog)
    {
        var conflicts = new List<GenerationConflict>();
        var s = rules.Document.Stats;

        // STATs: locked values plus the minimum of every other STAT must fit the budget.
        var locked = b.Locks.Stats.Where(k => b.Stats.ContainsKey(k)).Select(k => k.ToUpperInvariant()).Distinct().ToList();
        foreach (var k in locked.Where(k => b.Stats[k] > t.MaxStat.Value))
            conflicts.Add(new("STAT_ABOVE_MAX", L.T($"{k} travado em {b.Stats[k]} ultrapassa o máximo de {t.MaxStat.Value} para {t.DisplayName}.", $"{k} locked at {b.Stats[k]} goes over the maximum of {t.MaxStat.Value} for {t.DisplayName}."),
                L.T($"Reduza {k} para {t.MaxStat.Value} ou destrave o campo.", $"Lower {k} to {t.MaxStat.Value} or unlock the field."), t.MaxStat.Reference));
        var minimum = locked.Sum(k => b.Stats[k]) + rules.StatKeys.Where(k => !locked.Contains(k))
            .Sum(k => k == "EMP" ? s.EmpMin.Value : s.Min.Value);
        if (minimum > t.StatPoints.Value)
            conflicts.Add(new("STAT_BUDGET_EXCEEDED",
                L.T($"Os STATs travados somam {locked.Sum(k => b.Stats[k])}; com o mínimo nos demais, o total chega a {minimum}, acima do orçamento de {t.StatPoints.Value} em {minimum - t.StatPoints.Value}.", $"The locked STATs add up to {locked.Sum(k => b.Stats[k])}; with the minimum on the others, the total reaches {minimum}, {minimum - t.StatPoints.Value} over the budget of {t.StatPoints.Value}."),
                L.T("Reduza algum STAT travado ou destrave campos.", "Lower a locked STAT or unlock fields."), t.StatPoints.Reference));

        // Skills.
        var free = rules.FreeSkillLevels(b.NativeLanguage, b.Home);
        var skillCost = 0;
        foreach (var key in b.Locks.Skills.Select(SkillKey.Normalize).Where(k => b.Skills.ContainsKey(k)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (id, _) = SkillKey.Parse(key);
            if (rules.FindSkill(id) is not { } def)
            {
                conflicts.Add(new("SKILL_UNKNOWN", L.T($"Perícia travada \"{key}\" não existe.", $"Locked skill \"{key}\" doesn't exist."), "Remova a trava.", null));
                continue;
            }
            var level = b.Skills[key];
            if (level > t.MaxSkill.Value)
                conflicts.Add(new("SKILL_ABOVE_MAX", L.T($"A perícia {def.Name} travada em {level} ultrapassa o nível máximo de {t.MaxSkill.Value} permitido para esta categoria.", $"The {def.Name} skill locked at {level} goes over the maximum level of {t.MaxSkill.Value} allowed for this category."),
                    L.T($"Reduza para {t.MaxSkill.Value}.", $"Lower it to {t.MaxSkill.Value}."), t.MaxSkill.Reference));
            skillCost += Math.Max(0, level - free.GetValueOrDefault(key)) * rules.SkillCostMultiplier(def);
        }
        if (skillCost > t.SkillPoints.Value)
            conflicts.Add(new("SKILL_BUDGET_EXCEEDED", L.T($"As perícias travadas custam {skillCost} pontos, acima do orçamento de {t.SkillPoints.Value} em {skillCost - t.SkillPoints.Value}.", $"The locked skills cost {skillCost} points, {skillCost - t.SkillPoints.Value} over the budget of {t.SkillPoints.Value}."),
                L.T("Reduza perícias travadas.", "Lower locked skills."), t.SkillPoints.Reference));

        // Gear.
        var gearCost = 0;
        foreach (var e in b.Gear.Where(e => e.Locked))
        {
            if (catalog.Find(e.ItemId) is not { } item)
            {
                conflicts.Add(new("ITEM_NOT_FOUND", L.T($"Item fixo {e.ItemId} não existe no catálogo.", $"Fixed item {e.ItemId} doesn't exist in the catalog."), "Remova o item.", null));
                continue;
            }
            var (price, _, error) = _m.GearUnitPrice(item, e.Quality);
            if (error is not null) conflicts.Add(new("WEAPON_QUALITY_UNAVAILABLE", error, L.T("Mantenha a qualidade cadastrada.", "Keep the catalog quality."), rules.Document.WeaponQualityPricing.Source));
            gearCost += (price ?? 0) * e.Quantity;
        }
        if (gearCost > t.GearBudget.Value)
            conflicts.Add(new("GEAR_BUDGET_EXCEEDED",
                L.T($"O equipamento fixo custa {NpcValidator.Eb(gearCost)} e ultrapassa o orçamento de {NpcValidator.Eb(t.GearBudget.Value)} em {NpcValidator.Eb(gearCost - t.GearBudget.Value)}.", $"The fixed gear costs {NpcValidator.Eb(gearCost)} and goes {NpcValidator.Eb(gearCost - t.GearBudget.Value)} over the budget of {NpcValidator.Eb(t.GearBudget.Value)}."),
                L.T("Remova itens fixos ou troque por versões mais baratas.", "Remove fixed items or swap them for cheaper versions."), t.GearBudget.Reference));

        // Cyberware: required items must be installable with their dependencies, within budget, never forbidden.
        var forbidden = cyberRules.Where(r => r.State == CyberwareRuleState.Forbidden).Select(r => r.ItemId).ToHashSet();
        foreach (var e in b.Cyberware.Where(e => e.Locked && forbidden.Contains(e.ItemId)))
            conflicts.Add(new("CYBER_FORBIDDEN", L.T($"{catalog.Find(e.ItemId)?.Name ?? e.ItemId.ToString()} está fixo na ficha e também proibido.", $"{catalog.Find(e.ItemId)?.Name ?? e.ItemId.ToString()} is fixed on the sheet and also forbidden."), L.T("Escolha um dos dois.", "Pick one of the two."), null));

        var planner = new CyberwarePlanner(_m, catalog).WithForbidden(forbidden);
        planner.AddLocked(b.Cyberware.Where(e => e.Locked && catalog.Find(e.ItemId) is not null));
        foreach (var rule in cyberRules.Where(r => r.State == CyberwareRuleState.Required))
        {
            if (catalog.Find(rule.ItemId) is not { Cyber: not null } item)
            {
                conflicts.Add(new("ITEM_NOT_FOUND", L.T($"Implante obrigatório {rule.ItemId} não existe no catálogo.", $"Required implant {rule.ItemId} doesn't exist in the catalog."), null, null));
                continue;
            }
            if (planner.Contains(item.Id)) continue;
            var error = planner.TryInstall(item, null);
            if (error is not null)
                conflicts.Add(new("CYBER_REQUIRED_IMPOSSIBLE", L.T($"Não é possível instalar {item.Name}: {error}", $"Can't install {item.Name}: {error}"), L.T("Libere a dependência proibida ou remova a obrigatoriedade.", "Allow the forbidden dependency or drop the requirement."), null));
        }
        if (t.CyberwareBudget.Value is { } limit && planner.Spent > limit)
            conflicts.Add(new("CYBER_BUDGET_EXCEEDED",
                L.T($"O cyberware obrigatório excede o orçamento disponível: custa {NpcValidator.Eb(planner.Spent)} com as dependências, para um orçamento de {NpcValidator.Eb(limit)}.", $"The required cyberware exceeds the available budget: it costs {NpcValidator.Eb(planner.Spent)} with its dependencies, for a budget of {NpcValidator.Eb(limit)}."),
                L.T("Remova implantes obrigatórios ou use uma categoria com orçamento maior.", "Remove required implants or use a category with a bigger budget."), t.CyberwareBudget.Reference));

        return conflicts;
    }

    // ------------------------------------------------------------ STATs

    private Dictionary<string, int> DistributeStats(NpcTemplate t, NpcProfile profile, NpcSheet b, Random rng)
    {
        var s = rules.Document.Stats;
        var lockedKeys = b.Locks.Stats.Select(k => k.ToUpperInvariant()).Where(b.Stats.ContainsKey).ToHashSet();
        var stats = rules.StatKeys.ToDictionary(k => k, k => lockedKeys.Contains(k) ? b.Stats[k] : s.Min.Value, StringComparer.OrdinalIgnoreCase);

        // Tight budgets: lower EMP toward its own minimum before anything else.
        var remaining = t.StatPoints.Value - stats.Values.Sum();
        if (remaining < 0 && !lockedKeys.Contains("EMP"))
        {
            var cut = Math.Min(-remaining, stats["EMP"] - s.EmpMin.Value);
            stats["EMP"] -= cut;
            remaining += cut;
        }

        // Aim BODY + WILL at the suggested HP range (DGD p.151, Step Four): HP = 10 + 5 × ceil((BODY + WILL) / 2).
        var d = rules.Document.Derived;
        var minSum = 2 * (int)Math.Ceiling((t.SuggestedHp.Value.Min - d.HpBase.Value) / (double)d.HpPerAverage.Value) - 1;
        var maxSum = 2 * ((t.SuggestedHp.Value.Max - d.HpBase.Value) / d.HpPerAverage.Value);
        bool CanRaise(string k) => !lockedKeys.Contains(k) && stats[k] < t.MaxStat.Value;
        while (remaining > 0 && stats["BODY"] + stats["WILL"] < minSum && (CanRaise("BODY") || CanRaise("WILL")))
        {
            var k = !CanRaise("WILL") || (CanRaise("BODY") && stats["BODY"] <= stats["WILL"]) ? "BODY" : "WILL";
            stats[k]++;
            remaining--;
        }

        // Each point goes to a STAT drawn by the profile's weights, never past the template maximum
        // (nor past the suggested HP range, while other STATs can still take it).
        while (remaining > 0)
        {
            var open = rules.StatKeys.Where(CanRaise).ToList();
            var withinHp = open.Where(k => k is not ("BODY" or "WILL") || stats["BODY"] + stats["WILL"] < maxSum).ToList();
            if (withinHp.Count > 0) open = withinHp;
            if (open.Count == 0) break;
            var key = Weighted.Pick(open, k => profile.StatWeights.GetValueOrDefault(k, 1) * Jitter(rng), rng);
            stats[key]++;
            remaining--;
        }
        return stats;
    }

    private static double Jitter(Random rng) => 0.75 + rng.NextDouble() * 0.5;

    // ------------------------------------------------------------ skills

    private (Dictionary<string, int> Skills, List<string> KeySkills) DistributeSkills(
        NpcTemplate t, NpcProfile profile, NpcSheet b, Dictionary<string, int> stats, ArmorPenalty penalty,
        string nativeLanguage, string home, WeaponSkill? primarySkill, IReadOnlyList<NpcGearEntry> gear, NpcCatalog catalog, Random rng)
    {
        var free = rules.FreeSkillLevels(nativeLanguage, home);
        var skills = new Dictionary<string, int>(free, StringComparer.OrdinalIgnoreCase);
        var lockedKeys = b.Locks.Skills.Select(SkillKey.Normalize).Where(b.Skills.ContainsKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var k in lockedKeys) skills[k] = b.Skills[k];

        int Mult(string key) => rules.FindSkill(SkillKey.Parse(key).Id) is { } d ? rules.SkillCostMultiplier(d) : 1;
        int Cost(string key, int level) => Math.Max(0, level - free.GetValueOrDefault(key)) * Mult(key);
        int EffStat(string key)
        {
            var stat = rules.FindSkill(SkillKey.Parse(key).Id)?.Stat ?? "INT";
            var v = stats.GetValueOrDefault(stat);
            return Math.Max(0, v - stat switch { "REF" => penalty.Ref, "DEX" => penalty.Dex, "MOVE" => penalty.Move, _ => 0 });
        }
        var budget = t.SkillPoints.Value - skills.Sum(kv => Cost(kv.Key, kv.Value));

        // Important combat skills follow the weapons actually carried.
        var weaponSkillIds = rules.Document.WeaponSkillMap.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var primaryId = primarySkill is { } ps ? rules.SkillIdFor(ps) : null;
        var keyCombat = profile.KeyCombatSkills.Where(k => !weaponSkillIds.Contains(k) || primaryId is null || k == primaryId).ToList();
        if (primaryId is not null && !keyCombat.Contains(primaryId)) keyCombat.Insert(0, primaryId);
        var keyNonCombat = profile.KeyNonCombatSkills.ToList();

        void RaiseTo(string key, int targetBase)
        {
            if (lockedKeys.Contains(key)) return;
            var current = skills.GetValueOrDefault(key);
            var wanted = Math.Clamp(targetBase - EffStat(key), current, t.MaxSkill.Value);
            var affordable = current + Math.Max(0, budget) / Mult(key);
            var level = Math.Min(wanted, affordable);
            budget -= Cost(key, level) - Cost(key, current);
            skills[key] = level;
        }
        foreach (var k in keyCombat) RaiseTo(k, Weighted.Between(rng, t.CombatSkillBase.Value.Min, t.CombatSkillBase.Value.Max));
        foreach (var k in keyNonCombat) RaiseTo(k, Weighted.Between(rng, t.NonCombatSkillBase.Value.Min, t.NonCombatSkillBase.Value.Max));

        // Secondary skills: the profile's list, the skills of every carried weapon, and a couple of random flavor picks.
        var weights = new Dictionary<string, double>(profile.SecondarySkills, StringComparer.OrdinalIgnoreCase);
        foreach (var w in gear.Select(e => catalog.Find(e.ItemId)?.Weapon).Where(w => w is not null))
            if (rules.SkillIdFor(w!.Skill) is { } id) weights[id] = Math.Max(weights.GetValueOrDefault(id), 3);
        foreach (var extra in rules.Skills.Where(sd => !sd.Specialized && !sd.Combat).OrderBy(_ => rng.Next()).Take(2))
            weights.TryAdd(extra.Id, 0.6);
        foreach (var k in keyCombat.Concat(keyNonCombat)) weights.Remove(k);

        int Cap(string key)
        {
            var combat = rules.FindSkill(SkillKey.Parse(key).Id)?.Combat ?? false;
            var range = combat ? t.CombatSkillBase.Value : t.NonCombatSkillBase.Value;
            return Math.Min(t.MaxSkill.Value, Math.Max(skills.GetValueOrDefault(key), range.Max - EffStat(key)));
        }

        // Spend the rest one level at a time: secondaries up to the suggested range, then important skills
        // (never above their suggested base), and finally secondaries up to the template maximum.
        var started = weights.Keys.ToList();
        var passes = new (List<string> Pool, Func<string, int> Cap)[]
        {
            (weights.Keys.ToList(), Cap),
            (keyCombat.Concat(keyNonCombat).ToList(), Cap),
            (started, _ => t.MaxSkill.Value),
        };
        foreach (var (pool, cap) in passes)
        {
            while (budget > 0)
            {
                var open = pool.Where(k => !lockedKeys.Contains(k) && skills.GetValueOrDefault(k) < cap(k) && Mult(k) <= budget).ToList();
                if (open.Count == 0) break;
                var k = Weighted.Pick(open, x => weights.GetValueOrDefault(x, 1.5), rng);
                var current = skills.GetValueOrDefault(k);
                var level = Math.Max(current, free.GetValueOrDefault(k)) + 1;
                budget -= Cost(k, level) - Cost(k, current);
                skills[k] = level;
            }
        }

        var keySkills = keyCombat.Concat(keyNonCombat).Concat(b.KeySkills.Where(lockedKeys.Contains))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (skills, keySkills);
    }

    // ------------------------------------------------------------ helpers

    /// <summary>A gang draws among the profiles that suit it; without one, any profile is equally likely.</summary>
    private NpcProfile RandomProfile(NpcGang? gang, Random rng)
    {
        var suited = (gang?.Bias.Value?.Profiles ?? [])
            .Select(kv => (Profile: rules.FindProfile(kv.Key), Weight: kv.Value))
            .Where(p => p.Profile is not null && p.Weight > 0)
            .ToList();
        return suited.Count > 0 ? Weighted.Pick(suited, p => p.Weight, rng).Profile! : rules.Profiles[rng.Next(rules.Profiles.Count)];
    }

    private string PickHandle(NpcGang? gang, Random rng)
    {
        var g = rules.Document.Generator;
        if (gang?.Handles.Value is { Length: > 0 } own && rng.NextDouble() < g.GangHandleChance.Value)
            return own[rng.Next(own.Length)];
        return g.Handles[rng.Next(g.Handles.Length)];
    }

    private (string? Role, int Rank) PickRole(NpcTemplate t, NpcProfile profile, Random rng)
    {
        double P(RolePresence p) => rules.Document.RolePresence.GetValueOrDefault(p)?.Probability ?? 0;
        var rank = rng.NextDouble() < P(t.RoleRank.Value.Presence) ? t.RoleRank.Value.Rank : 0;
        if (t.HardenedRoleRank is { } h && rng.NextDouble() < P(h.Value.Presence)) rank = Math.Max(rank, h.Value.Rank);
        return rank > 0 && profile.Role is not null ? (profile.Role, rank) : (null, 0);
    }

    private static ArmorPenalty ArmorPenalties(IEnumerable<NpcGearEntry> gear, NpcCatalog catalog)
    {
        var worn = gear.Where(e => e.Worn is not null).Select(e => catalog.Find(e.ItemId)?.Armor).Where(a => a is not null).ToList();
        return new ArmorPenalty(
            worn.Select(a => a!.RefPenalty).DefaultIfEmpty(0).Max(),
            worn.Select(a => a!.DexPenalty).DefaultIfEmpty(0).Max(),
            worn.Select(a => a!.MovePenalty).DefaultIfEmpty(0).Max());
    }

    private static List<NpcCyberwareRule> MergeRules(IEnumerable<NpcCyberwareRule> request, IEnumerable<NpcCyberwareRule> sheet) =>
        request.Concat(sheet)
            .Where(r => r.State != CyberwareRuleState.Allowed)
            .GroupBy(r => r.ItemId)
            .Select(g => g.First())
            .ToList();

    private static NpcGenerationResult Fail(int seed, int attempts, string? profileId, List<GenerationConflict> conflicts) =>
        new(false, null, null, seed, attempts, profileId, conflicts);
}
