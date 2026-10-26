using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Creation;

/// <summary>
/// The three official creation methods (CP:R p.40): builds the draft, applies template rows and STAT rolls, resolves
/// skill specializations and, on finalize, hands over the Role's starting package or settles the Complete Package
/// budgets. Every value comes from <see cref="CharacterRuleBook"/>; nothing fixed by a template is changed silently.
/// </summary>
public sealed class CharacterCreationService(
    CharacterRuleBook rules, CharacterValidator validator, CyberwareInstaller installer, IDiceRoller dice)
{
    // ------------------------------------------------------------ draft

    public CharacterSheet NewDraft(CreateCharacterRequest request, CharacterChange change)
    {
        if (rules.FindRole(request.Role) is null) throw CharacterChange.Refuse("role", L.T($"Role desconhecida: {request.Role}.", $"Unknown Role: {request.Role}."));
        if (string.IsNullOrWhiteSpace(request.Name)) throw CharacterChange.Refuse("name", L.T("Informe o nome do personagem.", "Enter the character's name."));
        string? region = null;
        if (!string.IsNullOrWhiteSpace(request.CulturalRegion))
            region = rules.Document.Lifepath.CulturalOrigins.FirstOrDefault(o => o.Region.Equals(request.CulturalRegion.Trim(), StringComparison.OrdinalIgnoreCase))?.Region
                     ?? throw CharacterChange.Refuse("culturalRegion", L.T($"Origem cultural desconhecida: {request.CulturalRegion}.", $"Unknown cultural origin: {request.CulturalRegion}."));

        var identity = new CharacterIdentity
        {
            Name = request.Name,
            Handle = request.Handle,
            NativeLanguage = request.NativeLanguage,
            Home = request.Home ?? "Watson",
        }.Normalize();
        var sheet = new CharacterSheet
        {
            Identity = identity,
            Creation = new CreationState { Method = request.Method, Overrides = request.Overrides ?? new() },
            Lifepath = new LifepathState { CulturalRegion = region },
        };
        return Seed(sheet, request.Method, request.Role, change);
    }

    /// <summary>
    /// Restarts generation with another method or Role: purchases are refunded and STATs, skills and choices rebuilt.
    /// Identity, Lifepath, style and notes are kept.
    /// </summary>
    public CharacterSheet Reset(CharacterSheet sheet, CreationMethod method, string role, CharacterChange change)
    {
        RequireCreating(sheet);
        if (rules.FindRole(role) is null) throw CharacterChange.Refuse("role", L.T($"Role desconhecida: {role}.", $"Unknown Role: {role}."));
        if (sheet.Inventory.Count > 0 || sheet.Cyberware.Count > 0)
            change.Log(CharacterEventKind.Ledger, "creation-reset", L.T("Compras da criação desfeitas ao reiniciar a criação.", "Creation purchases undone when restarting creation."));
        var clean = sheet with
        {
            Inventory = [],
            Cyberware = [],
            Humanity = new HumanityState(),
            Wallet = new Wallet(),
            Creation = new CreationState { Method = method, Overrides = sheet.Creation.Overrides },
        };
        return Seed(clean, method, role, change);
    }

    private CharacterSheet Seed(CharacterSheet sheet, CreationMethod method, string role, CharacterChange change)
    {
        var start = rules.Document.Creation.RoleAbilityStartRank.Value;
        var home = sheet.Identity.Home;
        var skills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cash = 0;
        var m = rules.Document.Creation.Methods;

        switch (method)
        {
            case CreationMethod.Streetrat:
                // Template skills are fixed (CP:R p.86); "choose 1" specializations stay open until the player picks.
                foreach (var (k, v) in rules.RoleSkillLevels(role, home)) skills[k] = v;
                cash = m.Streetrat.StartingCash.Value;
                break;
            case CreationMethod.Edgerunner:
                // Every Role skill starts at the method minimum (CP:R p.88); the player spreads the rest of the 86.
                foreach (var k in rules.RoleSkillLevels(role, home).Keys) skills[k] = m.Edgerunner.SkillMin.Value;
                cash = m.Edgerunner.StartingCash.Value;
                break;
            default:
                // Book suggestion: 6 in every STAT with a couple of points left over (CP:R p.78).
                foreach (var k in rules.StatOrder) stats[k] = 6;
                foreach (var k in rules.BasicSkillKeys(home)) skills[k] = rules.Document.Creation.BasicSkillMin.Value;
                break;
        }
        if (CreationCosts.NativeLanguageKey(sheet) is { } native)
            skills[native] = Math.Max(skills.GetValueOrDefault(native), rules.Document.Creation.OriginLanguageLevel.Value);

        if (cash > 0)
            change.Log(CharacterEventKind.Ledger, "starting-cash",
                L.T($"Dinheiro inicial do {MethodLabel(method)} ({CharacterRuleBook.Page(rules.Document.Creation.Methods.Streetrat.StartingCash.Reference)}).", $"Starting money for {MethodLabel(method)} ({CharacterRuleBook.Page(rules.Document.Creation.Methods.Streetrat.StartingCash.Reference)})."), cash, "0", cash.ToString());

        return sheet with
        {
            Roles = [new CharacterRole { Role = role, Rank = start }],
            Stats = stats,
            Skills = skills,
            Wallet = new Wallet { Balance = cash },
        };
    }

    // ------------------------------------------------------------ STATs

    /// <summary>Streetrat: copies one row of the Role table, rolled on 1d10 when no row is given (CP:R p.73).</summary>
    public CharacterSheet ApplyTemplateRow(CharacterSheet sheet, int? row, CharacterChange change)
    {
        RequireCreating(sheet);
        if (sheet.Creation.Method != CreationMethod.Streetrat)
            throw CharacterChange.Refuse("method", L.T("Só o Streetrat usa uma linha inteira da tabela.", "Only Streetrat uses a whole table row."));
        var table = RoleRules(sheet).StatTable;
        var value = row ?? dice.Roll(table.Rows.Length);
        if (value < 1 || value > table.Rows.Length) throw CharacterChange.Refuse("row", L.T($"A linha deve estar entre 1 e {table.Rows.Length}.", $"The row must be between 1 and {table.Rows.Length}."));

        change.Log(CharacterEventKind.Edit, "template-row",
            L.T($"{(row is null ? "Rolou" : "Escolheu")} {value} na tabela de STATs de {sheet.PrimaryRole!.Role} ({table.Source}).", $"{(row is null ? "Rolled" : "Chose")} {value} on the {sheet.PrimaryRole!.Role} STAT table ({table.Source})."), value, manual: row is not null);
        return sheet with
        {
            Stats = rules.StatOrder.Select((k, i) => (k, table.Rows[value - 1][i])).ToDictionary(x => x.k, x => x.Item2, StringComparer.OrdinalIgnoreCase),
            Creation = sheet.Creation with { TemplateRow = value },
        };
    }

    /// <summary>Edgerunner: one 1d10 per STAT, each read in its own column of the Role table (CP:R p.77).</summary>
    public CharacterSheet ApplyStatRolls(CharacterSheet sheet, IReadOnlyDictionary<string, int>? given, CharacterChange change)
    {
        RequireCreating(sheet);
        if (sheet.Creation.Method != CreationMethod.Edgerunner)
            throw CharacterChange.Refuse("method", L.T("Só o Edgerunner rola cada STAT separadamente.", "Only Edgerunner rolls each STAT separately."));
        var table = RoleRules(sheet).StatTable;
        var rolls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rules.StatOrder.Count; i++)
        {
            var key = rules.StatOrder[i];
            var roll = given is not null && given.TryGetValue(key, out var g) ? g : dice.Roll(table.Rows.Length);
            if (roll < 1 || roll > table.Rows.Length) throw CharacterChange.Refuse($"rolls.{key}", L.T($"O dado de {key} deve estar entre 1 e {table.Rows.Length}.", $"The {key} die must be between 1 and {table.Rows.Length}."));
            rolls[key] = roll;
            stats[key] = table.Rows[roll - 1][i];
        }
        change.Log(CharacterEventKind.Edit, "stat-rolls",
            "STATs: " + string.Join(", ", rules.StatOrder.Select(k => $"{k} {stats[k]} (d{rolls[k]})")) + $" ({table.Source}).",
            manual: given is { Count: > 0 });
        return sheet with { Stats = stats, Creation = sheet.Creation with { StatRolls = rolls } };
    }

    // ------------------------------------------------------------ skills

    /// <summary>Picks the specialization of a template "choose 1" skill (Play Instrument, Science), keeping its level.</summary>
    public CharacterSheet ChooseSpecialization(CharacterSheet sheet, string skillId, string specialization, CharacterChange change)
    {
        RequireCreating(sheet);
        specialization = specialization.Trim();
        if (specialization.Length == 0 || specialization.StartsWith('@'))
            throw CharacterChange.Refuse("specialization", L.T("Informe a especialização.", "Enter the specialization."));
        if (!rules.RoleChoiceSkills(sheet.PrimaryRole!.Role).Contains(skillId, StringComparer.OrdinalIgnoreCase))
            throw CharacterChange.Refuse("skillId", L.T("Essa perícia não tem especialização em aberto no template da Role.", "That skill has no open specialization in the Role template."));

        var skills = new Dictionary<string, int>(sheet.Skills, StringComparer.OrdinalIgnoreCase);
        var old = sheet.Creation.SkillChoices.TryGetValue(skillId, out var prev) ? SkillKey.Make(skillId, prev) : SkillKey.Make(skillId, CharacterRuleBook.ChooseToken);
        var level = skills.Remove(old, out var l) ? l : 0;
        skills[SkillKey.Make(skillId, specialization)] = Math.Max(level, skills.GetValueOrDefault(SkillKey.Make(skillId, specialization)));
        var choices = new Dictionary<string, string>(sheet.Creation.SkillChoices, StringComparer.OrdinalIgnoreCase) { [skillId] = specialization };
        change.Log(CharacterEventKind.Edit, "skill-choice", $"{rules.FindSkill(skillId)?.Name ?? skillId}: {specialization}.");
        return sheet with { Skills = skills, Creation = sheet.Creation with { SkillChoices = choices } };
    }

    /// <summary>
    /// Keeps the free Cultural Origin language and Local Expert (Your Home) attached to the right specialization when
    /// the player edits the native language or home during creation.
    /// </summary>
    public CharacterSheet RekeyIdentitySkills(CharacterSheet before, CharacterSheet after)
    {
        var skills = new Dictionary<string, int>(after.Skills, StringComparer.OrdinalIgnoreCase);
        Move(before.Identity.NativeLanguage, after.Identity.NativeLanguage, "language");
        Move(before.Identity.Home, after.Identity.Home, "local-expert");
        if (CreationCosts.NativeLanguageKey(after) is { } native)
            skills[native] = Math.Max(skills.GetValueOrDefault(native), rules.Document.Creation.OriginLanguageLevel.Value);
        return after with { Skills = skills };

        void Move(string? from, string? to, string id)
        {
            if (from is null || to is null || from.Equals(to, StringComparison.OrdinalIgnoreCase)) return;
            var oldKey = SkillKey.Make(id, from);
            if (id == "language" && from.Equals("Streetslang", StringComparison.OrdinalIgnoreCase)) return;
            if (!skills.Remove(oldKey, out var level)) return;
            var newKey = SkillKey.Make(id, to);
            skills[newKey] = Math.Max(level, skills.GetValueOrDefault(newKey));
        }
    }

    // ------------------------------------------------------------ finalize

    /// <summary>
    /// Ends Character Generation. Refused while the validator reports blocking errors. Streetrats and Edgerunners
    /// receive the Role's weapons, armor, outfit and cyberware with the preset Humanity Loss (CP:R p.98–117);
    /// Complete Packages keep the unspent general budget and lose the unspent fashion budget (CP:R p.104–105).
    /// Everyone starts in the Role's housing with the first month paid (CP:R p.105).
    /// </summary>
    public CharacterSheet Finalize(CharacterSheet sheet, CharacterChange change)
    {
        RequireCreating(sheet);
        var report = validator.Validate(sheet, change.Catalog);
        if (!report.CanFinalize)
        {
            var first = report.Issues.Where(i => i.Severity == CharacterIssueSeverity.Error).Take(3).Select(i => i.Message);
            throw CharacterChange.Refuse("creation", L.T($"Ainda há {report.ErrorCount} pendência(s): ", $"There are still {report.ErrorCount} issue(s): ") + string.Join(" ", first));
        }

        if (sheet.Creation.Method is CreationMethod.Streetrat or CreationMethod.Edgerunner)
            sheet = HandOverStartingPackage(sheet, change);
        else
            sheet = SettleCompletePackage(sheet, report, change);

        var housing = rules.StartingHousingFor(sheet.PrimaryRole!.Role);
        var lifestyle = rules.FindLifestyle(housing.Lifestyle);
        change.Log(CharacterEventKind.Ledger, "starting-housing",
            L.T($"Moradia inicial: {rules.FindHousing(housing.Housing)?.Label} e lifestyle {lifestyle?.Label}, primeiro mês pago ({CharacterRuleBook.Page(housing.Source)}).", $"Starting housing: {rules.FindHousing(housing.Housing)?.Label} and lifestyle {lifestyle?.Label}, first month paid ({CharacterRuleBook.Page(housing.Source)})."), 0);
        change.Log(CharacterEventKind.Edit, "finalize", L.T($"Criação concluída ({MethodLabel(sheet.Creation.Method)}).", $"Creation complete ({MethodLabel(sheet.Creation.Method)})."));

        return sheet with
        {
            Creation = sheet.Creation with { Finalized = true, FinalizedAt = change.Now },
            Housing = sheet.Housing with
            {
                HousingId = sheet.Housing.HousingId ?? housing.Housing,
                LifestyleId = sheet.Housing.LifestyleId ?? housing.Lifestyle,
                PaidUntil = DateOnly.FromDateTime(change.Now).AddMonths(1),
            },
            Combat = new CombatState(),
        };
    }

    private CharacterSheet HandOverStartingPackage(CharacterSheet sheet, CharacterChange change)
    {
        var role = RoleRules(sheet);
        var inventory = new List<InventoryEntry>(sheet.Inventory);
        foreach (var (name, section) in Sections(role))
        {
            if (name == "cyberware") continue;
            foreach (var item in Picked(sheet, name, section))
            {
                var catalogItem = change.Catalog.Find(item.Slug)
                                  ?? throw CharacterChange.Refuse("creation", L.T($"Item inicial {item.Slug} não está no catálogo.", $"Starting item {item.Slug} isn't in the catalog."));
                change.Sheet = sheet with { Inventory = inventory };
                inventory.Add(new InventoryEntry
                {
                    InstanceId = change.NewId("it"),
                    ItemId = catalogItem.Id,
                    Quantity = item.Rounds is null ? item.Qty : 1,
                    Caliber = item.Caliber,
                    Rounds = item.Rounds,
                    State = item.Worn is not null || catalogItem.Weapon is not null ? ItemState.Equipped : ItemState.Carried,
                    Worn = item.Worn,
                    Origin = ItemOrigin.Starting,
                    PricePaid = 0,
                });
            }
        }
        sheet = sheet with { Inventory = inventory };

        // Foundational pieces go in first, so options listed before them (Interface Plugs before the Neural Link in the
        // Netrunner package) use the package's own foundation instead of making the planner add a second one.
        var cyber = Picked(sheet, "cyberware", role.Starting.Cyberware)
            .Select(i => (Item: i, Catalog: change.Catalog.Find(i.Slug)
                                            ?? throw CharacterChange.Refuse("creation", L.T($"Implante inicial {i.Slug} não está no catálogo.", $"Starting implant {i.Slug} isn't in the catalog."))))
            .OrderByDescending(x => x.Catalog.Cyber?.Foundational == true)
            .ToList();
        var lost = 0;
        foreach (var (item, catalogItem) in cyber)
        {
            var (plan, error) = installer.Plan(sheet, change.Catalog, catalogItem, new InstallOptions { MeatLimb = item.MeatArm, AtCreation = true });
            if (plan is null) throw CharacterChange.Refuse("creation", $"{catalogItem.Name}: {error}");
            var pieces = plan.Pieces.Select(p => p with { Origin = ItemOrigin.Starting, PricePaid = 0, InstalledAt = change.Now }).ToList();
            sheet = sheet with { Cyberware = [.. sheet.Cyberware, .. pieces] };
            foreach (var p in pieces)
            {
                lost += p.HumanityLossApplied;
                change.Log(CharacterEventKind.Humanity, "install-cyberware",
                    L.T($"{change.Catalog.Find(p.ItemId)?.Name} instalado (pacote inicial, HL pré-definida).", $"{change.Catalog.Find(p.ItemId)?.Name} installed (starting package, preset HL)."), -p.HumanityLossApplied);
            }
        }
        return sheet with { Humanity = sheet.Humanity with { Lost = sheet.Humanity.Lost + lost } };
    }

    private CharacterSheet SettleCompletePackage(CharacterSheet sheet, CharacterValidationReport report, CharacterChange change)
    {
        var balance = sheet.Wallet.Balance;
        if (report.Gear is { Remaining: > 0 } gear)
        {
            change.Log(CharacterEventKind.Ledger, "creation-budget-kept",
                L.T($"Sobra do orçamento de criação mantida ({CharacterRuleBook.Page(gear.Source)}).", $"Creation budget leftover kept ({CharacterRuleBook.Page(gear.Source)})."), gear.Remaining, balance.ToString(), (balance + gear.Remaining).ToString());
            balance += gear.Remaining;
        }
        if (report.Fashion is { Remaining: > 0 } fashion)
            change.Log(CharacterEventKind.Ledger, "fashion-budget-lost",
                L.T($"{CharacterValidator.Eb(fashion.Remaining)} do orçamento de moda não gastos foram perdidos ({CharacterRuleBook.Page(fashion.Source)}).", $"{CharacterValidator.Eb(fashion.Remaining)} of unspent fashion budget were lost ({CharacterRuleBook.Page(fashion.Source)})."), 0);

        // Purchases stay marked with the creation budget that paid them; from now on everything uses the balance.
        return sheet with { Wallet = sheet.Wallet with { Balance = balance } };
    }

    private static IEnumerable<(string Name, StartingSection Section)> Sections(RoleRules role) =>
        [("weapons", role.Starting.Weapons), ("outfit", role.Starting.Outfit), ("cyberware", role.Starting.Cyberware)];

    private static IEnumerable<StartingItem> Picked(CharacterSheet sheet, string name, StartingSection section)
    {
        for (var g = 0; g < section.Groups.Length; g++)
        {
            var options = section.Groups[g];
            var pick = options.Length == 1 ? 0 : sheet.Creation.StartingChoices.GetValueOrDefault($"{name}:{g}");
            foreach (var item in options[pick]) yield return item;
        }
    }

    // ------------------------------------------------------------ helpers

    private RoleRules RoleRules(CharacterSheet sheet) =>
        sheet.PrimaryRole is { } r && rules.FindRole(r.Role) is { } role
            ? role
            : throw CharacterChange.Refuse("role", L.T("Escolha a Role antes.", "Pick the Role first."));

    public static void RequireCreating(CharacterSheet sheet)
    {
        if (sheet.Creation.Finalized) throw CharacterChange.Refuse("creation", L.T("A criação deste personagem já foi concluída.", "This character's creation is already complete."));
    }

    private string MethodLabel(CreationMethod method) => method switch
    {
        CreationMethod.Streetrat => rules.Document.Creation.Methods.Streetrat.Label,
        CreationMethod.Edgerunner => rules.Document.Creation.Methods.Edgerunner.Label,
        _ => rules.Document.Creation.Methods.CompletePackage.Label,
    };
}
