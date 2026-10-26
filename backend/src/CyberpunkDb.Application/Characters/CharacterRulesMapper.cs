using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters;

/// <summary>Turns the rule books into the shape the character screens use. No rule is decided here.</summary>
internal static class CharacterRulesMapper
{
    public static CharacterRulesDto Map(CharacterRuleBook rules, NpcCatalog catalog)
    {
        var d = rules.Document;
        var m = d.Creation.Methods;
        var methods = new List<CreationMethodDto>
        {
            new(CreationMethod.Streetrat, m.Streetrat.Label, m.Streetrat.Summary.Value, m.Streetrat.Summary.Reference,
                null, null, null, null, null, null, null, null, m.Streetrat.StartingCash.Value, []),
            new(CreationMethod.Edgerunner, m.Edgerunner.Label, m.Edgerunner.Summary.Value, m.Edgerunner.Summary.Reference,
                null, null, null, m.Edgerunner.SkillPoints.Value, m.Edgerunner.SkillMin.Value, m.Edgerunner.SkillMax.Value,
                null, null, m.Edgerunner.StartingCash.Value, []),
            new(CreationMethod.CompletePackage, m.CompletePackage.Label, m.CompletePackage.Summary.Value, m.CompletePackage.Summary.Reference,
                m.CompletePackage.StatPoints.Value, m.CompletePackage.StatMin.Value, m.CompletePackage.StatMax.Value,
                m.CompletePackage.SkillPoints.Value, null, m.CompletePackage.SkillMax.Value,
                m.CompletePackage.GearBudget.Value, m.CompletePackage.FashionBudget.Value, null, m.CompletePackage.StatPresets.Value),
        };

        var roles = d.Roles.Select(r => new RoleDto(
            r.Id, r.Ability.Name, r.Ability.Page, r.StatTable.Rows, r.StatTable.Source,
            r.Skills.Levels, r.Skills.Source, rules.RoleChoiceSkills(r.Id),
            [
                Section("weapons", "Armas e armadura", r.Starting.Weapons, catalog),
                Section("outfit", "Roupa e equipamento", r.Starting.Outfit, catalog),
                Section("cyberware", "Cyberware", r.Starting.Cyberware, catalog),
            ],
            r.Lifepath.Questions.Select(q => new LifepathQuestionDto(q.Id, q.Label, q.LabelPt, q.Dice, q.Options, q.ShowIf?.Question, q.ShowIf?.Value)).ToList(),
            r.Lifepath.Source,
            new RoleAbilityDto(r.Ability.Name, r.Ability.Source, r.Ability.Summary, r.Ability.Points))).ToList();

        var skills = rules.Skills.Select(s => new SkillDto(s.Id, s.Name, s.Stat, s.Category, s.Combat, s.DoubleCost, s.Specialized, s.Page, s.NamePt)).ToList();

        var lp = d.Lifepath;
        var relations = new LifepathRelationsDto(lp.Friends.Relationship, lp.Enemies.Who, lp.Enemies.Cause, lp.Enemies.Resources,
            lp.Enemies.Revenge.Options, lp.TragicLoveAffairs.Outcome, lp.Source);

        var injuries = d.Combat.CriticalInjuries.Body.Select(i => Injury(InjuryLocation.Body, i))
            .Concat(d.Combat.CriticalInjuries.Head.Select(i => Injury(InjuryLocation.Head, i)))
            .ToList();

        return new CharacterRulesDto(
            d.About, d.Books.Concat(rules.Shared.Document.Books).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.First().Value),
            d.StatOrder, methods, roles, skills, d.Creation.BasicSkills.Value, d.Creation.OriginLanguageLevel.Value,
            lp.CulturalOrigins.Select(o => new CulturalOriginDto(o.Region, o.Languages)).ToList(),
            rules.Shared.Document.Generator.Homes,
            lp.Tables.Select(t => new LifepathTableDto(t.Id, t.Label, t.LabelPt, t.Dice, t.Page, t.Options)).ToList(),
            relations, injuries,
            d.Economy.Lifestyles.Options.Select(o => new NamedValueDto(o.Id, o.Label, o.Monthly)).ToList(),
            d.Economy.Housing.Options.Select(o => new HousingDto(o.Id, o.Label, o.Rent, o.Buy, o.Corporate)).ToList(),
            Facts(rules),
            new CyberwareRulesDto(
                d.Cyberware.MeatArmOptions.Value, d.Cyberware.MeatLegOptions.Value,
                d.Cyberware.AnyLimbOptions.Value.Select(o => o.Slug).ToList(),
                d.Cyberware.AnyLimbOptions.Value.Where(o => o.Covering).Select(o => o.Slug).ToList(),
                d.Therapy.Kinds.Select(k => new TherapyKindDto(k.Id, k.Label, k.Cost, k.Recovery, k.Dv, k.Materials, k.Source)).ToList(),
                d.Therapy.Payment.Value, CharacterRuleBook.Page(d.Therapy.Payment.Reference)));
    }

    private static StartingSectionDto Section(string key, string label, StartingSection section, NpcCatalog catalog) =>
        new(key, label, section.Source, section.HumanityLoss,
            section.Groups.Select((g, gi) => new StartingGroupDto(gi, g.Select((o, oi) =>
            {
                var items = o.Select(i =>
                {
                    var item = catalog.Find(i.Slug);
                    return new StartingItemDto(i.Slug, item?.Id, item?.Name ?? i.Slug, i.Qty, i.Caliber, i.Rounds, i.Worn, i.MeatArm);
                }).ToList();
                return new StartingOptionDto(oi, string.Join(" + ", items.Select(Describe)), items);
            }).ToList())).ToList());

    private static string Describe(StartingItemDto i) =>
        i.Rounds is { } r ? $"{i.Name} ({i.Caliber}) x{r}"
        : i.Qty > 1 ? $"{i.Name} x{i.Qty}"
        : i.Worn is { } w ? $"{i.Name} ({(w == ArmorSlot.Head ? "cabeça" : "corpo")})"
        : i.MeatArm ? L.T($"{i.Name} (braço de carne)", $"{i.Name} (meat arm)")
        : i.Name;

    private static CriticalInjuryDto Injury(InjuryLocation location, CriticalInjuryDefinition i) =>
        new(location, i.Roll, i.Name, i.Effect, i.QuickFix, i.Treatment, i.DeathSave, i.QuickFixHeals);

    private static List<RuleFactDto> Facts(CharacterRuleBook rules)
    {
        var d = rules.Document;
        var shared = rules.Shared.Document.Derived;
        RuleFactDto Fact<T>(string key, string label, Ruled<T> r, string? value = null) =>
            new(key, label, value ?? r.Value?.ToString() ?? "", r.Source, r.Complementary, r.Note);
        return
        [
            Fact("hp", L.T("Pontos de Vida", "Hit Points"), shared.HpBase, L.T("10 + 5 × média de BODY e WILL (arredondada para cima)", "10 + 5 × average of BODY and WILL (rounded up)")),
            Fact("seriously", "Seriously Wounded", d.Derived.SeriouslyWounded,
                L.T($"menos que metade do HP (arred. p/ cima): {d.Derived.SeriouslyWounded.Value.ActionPenalty} em todas as Ações", $"less than half HP (rounded up): {d.Derived.SeriouslyWounded.Value.ActionPenalty} to all Actions")),
            Fact("mortally", "Mortally Wounded", d.Derived.MortallyWounded,
                L.T($"menos de 1 HP: {d.Derived.MortallyWounded.Value.ActionPenalty} em Ações, {d.Derived.MortallyWounded.Value.MovePenalty} em MOVE (mín. 1)", $"less than 1 HP: {d.Derived.MortallyWounded.Value.ActionPenalty} to Actions, {d.Derived.MortallyWounded.Value.MovePenalty} to MOVE (min. 1)")),
            Fact("deathSave", "Death Save", d.Derived.DeathSave, L.T("1d10 + penalidade abaixo de BODY; 10 natural sempre falha; +1 a cada Death Save", "1d10 + penalty under BODY; a natural 10 always fails; +1 per Death Save")),
            Fact("humanity", "Humanidade", d.Derived.HumanityPerEmp, L.T("EMP × 10; EMP efetiva = dezena da Humanidade atual", "EMP × 10; effective EMP = current Humanity's tens digit")),
            Fact("therapy", "Terapia", d.Therapy.MaxHumanityPerPiece, L.T("teto de Humanidade −2 por peça de cyberware, −4 por borgware", "Humanity cap −2 per cyberware piece, −4 per borgware")),
            Fact("ablation", L.T("Ablação", "Ablation"), d.Combat.AblationPerDamagingHit, L.T("−1 SP em toda armadura do local sempre que o dano passa", "−1 SP to all armor on the location whenever damage gets through")),
            Fact("armorPenalty", L.T("Penalidade de armadura", "Armor penalty"), d.Combat.ArmorPenaltyOnce, L.T("a mais pesada, aplicada uma vez", "the heaviest, applied once")),
            Fact("quickFix", "Quick Fix", d.Combat.QuickFix),
            Fact("treatment", "Treatment", d.Combat.Treatment),
            Fact("ip", "Improvement Points", d.Progression.IpSkillPerLevel, L.T("perícia: 20 × próximo nível (x2: 40); Role: 60 × próximo Rank", "skill: 20 × next level (x2: 40); Role: 60 × next Rank")),
            Fact("roleLifepath", L.T("Lifepath da Role no multiclasse", "Role Lifepath when multiclassing"), d.Progression.RoleLifepathPerRole, L.T("um por Role; o da Role nova é opcional", "one per Role; the new Role's is optional")),
        ];
    }
}
