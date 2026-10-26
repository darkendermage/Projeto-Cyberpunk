using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Calculation;

/// <summary>
/// Computes every derived value of a character from its sheet: effective STATs, HP and Wound State, Death Save,
/// Humanity and therapy cap, armor SP and penalty, and Skill Bases. Pure: the sheet is never modified, so base
/// values are never overwritten by modifiers.
/// </summary>
public sealed class CharacterCalculator(CharacterRuleBook rules)
{
    private const string Luck = "LUCK";

    public CharacterReport Calculate(CharacterSheet sheet, NpcCatalog catalog, DateOnly? today = null)
    {
        var armor = ComputeArmor(sheet, catalog);
        var injuries = ActiveInjuries(sheet);
        var effects = CyberwareEffects.Active(rules, sheet, catalog);
        var body = StatWithCyberware(sheet, catalog, effects, "BODY");

        // HP depends on BODY and WILL, which armor never penalizes; MOVE penalties from wounds need the HP first.
        var will = StatWithCyberware(sheet, catalog, effects, "WILL").Value;
        var maxHp = HitPoints(body.Value, will);
        var health = ComputeHealth(sheet, maxHp, body.Value, body.CyberSource, injuries);
        var humanity = ComputeHumanity(sheet, catalog);

        var stats = ComputeStats(sheet, catalog, effects, armor, health, injuries, humanity);
        var effective = stats.ToDictionary(s => s.Key, s => s.Effective, StringComparer.OrdinalIgnoreCase);

        var actionParts = new List<ValuePart>();
        if (health.WoundActionPenalty != 0) actionParts.Add(new ValuePart(WoundLabel(health.WoundState), health.WoundActionPenalty));
        foreach (var i in injuries.Where(i => i.Definition?.ActionPenalty is not null))
            actionParts.Add(new ValuePart(i.Record.Name, i.Definition!.ActionPenalty!.Value));
        foreach (var m in sheet.Modifiers.Where(m => m.Active && m.Target == ModifierTarget.AllActions))
            actionParts.Add(new ValuePart(m.Source, m.Value));
        var actionPenalty = actionParts.Sum(p => p.Value);

        var luck = sheet.Stat(Luck);
        var skills = ComputeSkills(sheet, effective, actionPenalty, effects);
        return new CharacterReport(
            stats,
            health,
            humanity,
            armor,
            skills,
            ComputeRoles(sheet),
            injuries.Select(i => new ActiveInjuryLine(i.Record.Id, i.Record.Name, i.Record.Location, i.Record.Status,
                i.Definition?.Effect, i.Definition?.DeathSave ?? false)).ToList(),
            actionParts,
            actionPenalty,
            luck,
            Math.Max(0, luck - sheet.Combat.LuckSpent),
            InventoryLines(sheet, catalog),
            WeaponLines(sheet, catalog, skills),
            CyberLines(sheet, catalog),
            ComputeHousing(sheet, today ?? DateOnly.FromDateTime(DateTime.UtcNow)),
            CyberwareAnatomy.Describe(sheet, catalog, Mechanics(sheet, catalog)),
            rules.InFbc(sheet, catalog),
            CyberwareEffects.Lines(rules, effects),
            WearableCyberware.Lines(rules, sheet, catalog));
    }

    // ------------------------------------------------------------ housing and lifestyle

    /// <summary>
    /// Monthly cost and payment status. Unpaid lifestyle has a one-week grace before daily Death Saves (CP:R p.377);
    /// the sheet only reports it, it never charges or penalizes on its own.
    /// </summary>
    public HousingReport ComputeHousing(CharacterSheet sheet, DateOnly today)
    {
        var h = sheet.Housing;
        var housing = rules.FindHousing(h.HousingId);
        var lifestyle = rules.FindLifestyle(h.LifestyleId);
        var rent = h.RentOverride ?? housing?.Rent ?? 0;
        var monthly = rent + (lifestyle?.Monthly ?? 0);
        var source = $"{rules.Document.Economy.Lifestyles.Source}; {rules.Document.Economy.Housing.Source}";

        if (housing is null && lifestyle is null && h.PaidUntil is null)
            return new HousingReport(null, null, null, null, 0, true, 0, 0, null, 0, PaymentStatus.NotApplicable, source);

        var overdue = h.PaidUntil is { } paid ? Math.Max(0, today.DayNumber - paid.DayNumber) : 0;
        var grace = rules.Document.Economy.LifestyleGraceDays.Value;
        var status = monthly == 0 ? PaymentStatus.NotApplicable
            : h.PaidUntil is null ? PaymentStatus.Pending
            : overdue == 0 ? PaymentStatus.Paid
            : overdue <= grace ? PaymentStatus.Pending
            : PaymentStatus.Overdue;
        return new HousingReport(h.HousingId, housing?.Label ?? h.HousingId, h.LifestyleId, lifestyle?.Label ?? h.LifestyleId,
            rent, h.RentOverride is null, lifestyle?.Monthly ?? 0, monthly, h.PaidUntil, overdue, status, source);
    }

    // ------------------------------------------------------------ item lines shown on the sheet

    private List<InventoryLine> InventoryLines(CharacterSheet sheet, NpcCatalog catalog) =>
        sheet.Inventory.Select(e =>
        {
            var item = catalog.Find(e.ItemId);
            var isShield = item?.Armor?.Type == ArmorType.Shield;
            var shieldMax = isShield ? rules.Document.Combat.ShieldHp.Value : (int?)null;
            return new InventoryLine(
                e.InstanceId, e.ItemId, item?.Name ?? $"Item {e.ItemId}", item?.Slug ?? "", item?.Category ?? ItemCategory.Gear,
                e.Quantity, e.State, e.Worn, e.Caliber, e.Rounds,
                item?.Armor?.Sp, item?.Armor?.Sp is { } sp ? e.CurrentSp ?? sp : null,
                isShield ? e.CurrentHp ?? shieldMax : null, shieldMax,
                item?.Price, e.PricePaid, e.Origin, e.PaidFrom,
                item is not null && Validation.CharacterValidator.IsFashion(item), e.Notes);
        }).ToList();

    private List<WeaponLine> WeaponLines(CharacterSheet sheet, NpcCatalog catalog, List<CharacterSkillLine> skills)
    {
        var lines = new List<WeaponLine>();
        var slots = rules.Document.Combat.AttachmentSlots.Value;
        foreach (var e in sheet.Inventory)
        {
            if (catalog.Find(e.ItemId) is not { Weapon: { } w } item) continue;
            var skillId = rules.Shared.SkillIdFor(w.Skill);
            var skill = skills.FirstOrDefault(s => s.Key.Equals(skillId, StringComparison.OrdinalIgnoreCase));
            var autofire = w.AutofireMultiplier is not null ? skills.FirstOrDefault(s => s.Key == "autofire") : null;
            var state = e.Weapon ?? new WeaponState();
            var attachments = state.Attachments
                .Select(id => sheet.Inventory.FirstOrDefault(i => i.InstanceId == id))
                .Select(i => i is null ? "?" : catalog.Find(i.ItemId)?.Name ?? "?")
                .ToList();
            var ranged = w.Calibers.Count > 0;
            lines.Add(new WeaponLine(
                e.InstanceId, e.ItemId, item.Name, w.Class, skillId, skill?.NamePt ?? skill?.Name,
                skill?.Total ?? 0, w.Damage, w.Rof, w.Magazine, state.LoadedRounds, state.LoadedAmmoItemId,
                state.LoadedAmmoItemId is { } ammo ? catalog.Find(ammo)?.Name : null,
                w.AutofireMultiplier, autofire?.Total, w.Calibers, w.Exotic,
                ranged && !w.Exotic ? slots : 0, attachments, e.State == ItemState.Equipped));
        }
        return lines;
    }

    /// <summary>Slot rules for this character: doubled in a Full Body Conversion body (Interface RED 3 p.93).</summary>
    private NpcMechanics Mechanics(CharacterSheet sheet, NpcCatalog catalog) =>
        new(rules.Shared, rules.InFbc(sheet, catalog) ? rules.Document.Cyberware.Fbc.Value.SlotMultiplier : 1);

    private CyberLine[] CyberLines(CharacterSheet sheet, NpcCatalog catalog)
    {
        var mechanics = Mechanics(sheet, catalog);
        var covered = WearableCyberware.Covered(rules, sheet, catalog);
        return sheet.Cyberware.Select(c =>
        {
            var item = catalog.Find(c.ItemId);
            var used = sheet.Cyberware.Where(o => o.ParentInstanceId == c.InstanceId)
                .Sum(o => catalog.Find(o.ItemId) is { } oi ? mechanics.SlotsRequired(oi).Slots : 1);
            return new CyberLine(
                c.InstanceId, c.ItemId, item?.Name ?? $"Item {c.ItemId}", item?.Slug ?? "", item?.Cyber?.Type, item?.Cyber?.Location,
                c.ParentInstanceId, c.Side, c.MeatLimb, item?.Cyber?.Foundational ?? false,
                item is null ? null : mechanics.SlotsProvided(item), used, c.HumanityLossApplied, item?.Cyber?.HumanityLossDice,
                c.CurrentSp, CyberwareAnatomy.RegionOf(c, sheet.Cyberware, catalog), item?.Price ?? 0,
                item is null ? null : mechanics.FreeTextRequirement(item, catalog), covered.GetValueOrDefault(c.InstanceId), item?.Cyber?.Effect);
        }).ToArray();
    }

    // ------------------------------------------------------------ STATs

    private List<StatLine> ComputeStats(
        CharacterSheet sheet, NpcCatalog catalog, List<ActiveCyberwareEffect> effects,
        ArmorReport armor, HealthReport health, List<ActiveInjury> injuries, HumanityReport humanity)
    {
        var penalized = rules.Shared.Document.Stats.ArmorPenalized;
        var lines = new List<StatLine>();
        foreach (var key in rules.StatOrder)
        {
            var baseValue = sheet.Stat(key);
            var (beforePenalties, parts, _) = StatWithCyberware(sheet, catalog, effects, key);
            var armorPart = 0;

            if (penalized.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var p = key.ToUpperInvariant() switch
                {
                    "REF" => armor.Penalty.Ref,
                    "DEX" => armor.Penalty.Dex,
                    "MOVE" => armor.Penalty.Move,
                    _ => 0,
                };
                if (p != 0) parts.Add(new ValuePart(armor.PenaltySource ?? "Armadura", -p));
                armorPart = -p;
            }

            var effective = Math.Max(0, beforePenalties + armorPart);

            if (key.Equals("MOVE", StringComparison.OrdinalIgnoreCase))
            {
                // Wound and injury MOVE penalties never take MOVE below 1 (CP:R p.186–187); armor alone can.
                var floor = rules.Document.Derived.MortallyWounded.Value.MoveMinimum;
                var limited = new List<ValuePart>();
                if (health.WoundMovePenalty != 0) limited.Add(new ValuePart(WoundLabel(health.WoundState), health.WoundMovePenalty));
                foreach (var i in injuries.Where(i => i.Definition?.MovePenalty is not null))
                    limited.Add(new ValuePart(i.Record.Name, i.Definition!.MovePenalty!.Value));
                if (limited.Count > 0)
                {
                    var after = effective + limited.Sum(x => x.Value);
                    effective = effective <= floor ? effective : Math.Max(floor, after);
                    parts.AddRange(limited);
                }
            }

            if (key.Equals("EMP", StringComparison.OrdinalIgnoreCase) && humanity.EmpEffective != baseValue)
            {
                // EMP follows the tens place of the current Humanity (CP:R p.80); modifiers don't raise it above that.
                parts.Add(new ValuePart("Humanidade", humanity.EmpEffective - baseValue));
                effective = humanity.EmpEffective;
            }

            lines.Add(new StatLine(key, baseValue, effective, parts));
        }
        return lines;
    }

    /// <summary>
    /// A STAT before armor and wounds: base + active modifiers + cyberware bonuses (Grafted Muscle and Bone Lace, up to
    /// its cap, CP:R p.363) and, for BODY, an Implanted Linear Frame when it is higher (CP:R p.367). The base is never
    /// changed. <c>CyberSource</c> names the implants that raised it.
    /// </summary>
    private static (int Value, List<ValuePart> Parts, string? CyberSource) StatWithCyberware(
        CharacterSheet sheet, NpcCatalog catalog, List<ActiveCyberwareEffect> effects, string key)
    {
        var parts = sheet.Modifiers
            .Where(m => m.Active && m.Target == ModifierTarget.Stat && m.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(m => new ValuePart(m.Source, m.Value)).ToList();
        var value = sheet.Stat(key) + parts.Sum(p => p.Value);
        var raisedBy = new List<string>();

        foreach (var a in effects.Where(a => a.Effect.Stat?.Equals(key, StringComparison.OrdinalIgnoreCase) == true))
        {
            var gain = a.Effect.Max is { } max ? Math.Clamp(max - value, 0, a.Bonus) : a.Bonus;
            parts.Add(new ValuePart(gain < a.Bonus ? $"{a.Label} (teto {a.Effect.Max})" : a.Label, gain));
            value += gain;
            if (gain > 0) raisedBy.Add(a.Label);
        }

        if (key.Equals("BODY", StringComparison.OrdinalIgnoreCase))
        {
            var frame = sheet.Cyberware.Select(c => catalog.Find(c.ItemId))
                .Where(i => i?.Armor is { Implanted: true, BodyValue: not null })
                .MaxBy(i => i!.Armor!.BodyValue);
            if (frame is not null && frame.Armor!.BodyValue!.Value > value)
            {
                parts.Add(new ValuePart(frame.Name, frame.Armor.BodyValue.Value - value));
                value = frame.Armor.BodyValue.Value;
                raisedBy.Add(frame.Name);
            }
        }
        return (value, parts, raisedBy.Count > 0 ? string.Join(", ", raisedBy) : null);
    }

    // ------------------------------------------------------------ HP, wounds, Death Save

    /// <summary>HP = 10 + 5 × ceil((BODY + WILL) / 2) (CP:R p.79).</summary>
    public int HitPoints(int body, int will)
    {
        var d = rules.Shared.Document.Derived;
        return d.HpBase.Value + d.HpPerAverage.Value * (int)Math.Ceiling((body + will) / 2.0);
    }

    /// <summary>Half of max HP, rounded up (CP:R p.79).</summary>
    public int SeriouslyWoundedThreshold(int maxHp)
    {
        var r = rules.Document.Derived.SeriouslyWounded.Value;
        var raw = maxHp * r.Fraction;
        return r.RoundUp ? (int)Math.Ceiling(raw) : (int)Math.Floor(raw);
    }

    public WoundState WoundStateFor(int currentHp, int maxHp)
    {
        var serious = rules.Document.Derived.SeriouslyWounded.Value;
        var mortal = rules.Document.Derived.MortallyWounded.Value;
        var threshold = SeriouslyWoundedThreshold(maxHp);
        if (currentHp < mortal.BelowHp) return WoundState.Mortally;
        var isSerious = serious.Comparison == ThresholdComparison.LessThanOrEqual ? currentHp <= threshold : currentHp < threshold;
        if (isSerious) return WoundState.Seriously;
        return currentHp < maxHp ? WoundState.Lightly : WoundState.Unhurt;
    }

    private HealthReport ComputeHealth(CharacterSheet sheet, int maxHp, int body, string? bodySource, List<ActiveInjury> injuries)
    {
        var current = Math.Min(sheet.Combat.CurrentHp ?? maxHp, maxHp);
        var state = WoundStateFor(current, maxHp);
        var d = rules.Document.Derived;
        var (action, move) = state switch
        {
            WoundState.Seriously => (d.SeriouslyWounded.Value.ActionPenalty, 0),
            WoundState.Mortally => (d.MortallyWounded.Value.ActionPenalty, d.MortallyWounded.Value.MovePenalty),
            _ => (0, 0),
        };
        int? dv = state switch
        {
            WoundState.Lightly => d.StabilizationDv.Value.Lightly,
            WoundState.Seriously => d.StabilizationDv.Value.Seriously,
            WoundState.Mortally => d.StabilizationDv.Value.Mortally,
            _ => null,
        };
        var basePenalty = injuries.Count(i => i.Definition?.DeathSave == true);
        return new HealthReport(
            maxHp, current, SeriouslyWoundedThreshold(maxHp), state, action, move, dv,
            body, bodySource, body, basePenalty, basePenalty + sheet.Combat.DeathSavePenalty,
            state == WoundState.Mortally);
    }

    private static string WoundLabel(WoundState state) => state switch
    {
        WoundState.Seriously => "Seriously Wounded",
        WoundState.Mortally => "Mortally Wounded",
        WoundState.Lightly => "Lightly Wounded",
        _ => "Sem ferimentos",
    };

    // ------------------------------------------------------------ Humanity

    private HumanityReport ComputeHumanity(CharacterSheet sheet, NpcCatalog catalog)
    {
        var empBase = sheet.Stat("EMP");
        var max = empBase * rules.Document.Derived.HumanityPerEmp.Value;
        var current = max - sheet.Humanity.Lost + sheet.Humanity.Recovered;

        var t = rules.Document.Therapy;
        var fbc = rules.InFbc(sheet, catalog);
        var pieces = 0;
        var borgware = 0;
        foreach (var c in sheet.Cyberware)
        {
            var item = catalog.Find(c.ItemId);
            var zeroLoss = c.HumanityLossApplied == 0 && (item?.Cyber?.HumanityLoss ?? 0) == 0;
            if (zeroLoss && t.ZeroLossPiecesExempt.Value) continue;
            // In an FBC body these foundations don't depress the maximum Humanity (Interface RED 3 p.93).
            if (fbc && item is not null && rules.IsFbcExempt(item.Slug)) continue;
            if (item?.Cyber?.Type == CyberwareType.Borgware) borgware++;
            else pieces++;
        }
        var cap = max - pieces * t.MaxHumanityPerPiece.Value - borgware * t.MaxHumanityPerBorgware.Value;
        var empEffective = Math.Max(0, current) / 10;
        return new HumanityReport(empBase, max, current, sheet.Humanity.Lost, sheet.Humanity.Recovered, cap, pieces, borgware,
            empEffective, current < rules.Document.Derived.CyberpsychosisBelow.Value);
    }

    // ------------------------------------------------------------ armor

    private ArmorReport ComputeArmor(CharacterSheet sheet, NpcCatalog catalog)
    {
        var worn = sheet.Inventory
            .Where(e => e.State == ItemState.Equipped && e.Worn is not null)
            .Select(e => (Entry: e, Item: catalog.Find(e.ItemId)))
            .Where(x => x.Item?.Armor is { Type: ArmorType.BodyArmor })
            .ToList();

        var implants = sheet.Cyberware
            .Select(c => (Entry: c, Item: catalog.Find(c.ItemId)))
            .Where(x => x.Item is not null && rules.Shared.CyberwareStoppingPower(x.Item.Slug) is not null)
            .Select(x => (x.Entry, Item: x.Item!, Max: rules.Shared.CyberwareStoppingPower(x.Item!.Slug)!.Value))
            .ToList();

        ArmorLocationReport Location(ArmorSlot slot)
        {
            // SP doesn't stack: only the highest source in a location counts (CP:R p.184).
            var candidates = worn.Where(x => x.Entry.Worn == slot)
                .Select(x => new ArmorLocationReport(slot, x.Entry.CurrentSp ?? x.Item!.Armor!.Sp ?? 0, x.Item!.Armor!.Sp,
                    x.Entry.InstanceId, x.Item.Name, false))
                .Concat(implants.Select(x => new ArmorLocationReport(slot, x.Entry.CurrentSp ?? x.Max, x.Max, x.Entry.InstanceId, x.Item.Name, true)))
                .ToList();
            return candidates.MaxBy(c => c.Sp) ?? new ArmorLocationReport(slot, 0, null, null, null, false);
        }

        // The most punishing penalty among worn armor, taken once (CP:R p.184).
        var refP = worn.Select(x => x.Item!.Armor!.RefPenalty).DefaultIfEmpty(0).Max();
        var dexP = worn.Select(x => x.Item!.Armor!.DexPenalty).DefaultIfEmpty(0).Max();
        var moveP = worn.Select(x => x.Item!.Armor!.MovePenalty).DefaultIfEmpty(0).Max();
        var heaviest = worn.Select(x => (x.Item!.Name, Total: x.Item.Armor!.RefPenalty + x.Item.Armor.DexPenalty + x.Item.Armor.MovePenalty))
            .Where(x => x.Total > 0)
            .OrderByDescending(x => x.Total)
            .Select(x => x.Name)
            .FirstOrDefault();

        return new ArmorReport(Location(ArmorSlot.Head), Location(ArmorSlot.Body), new ArmorPenalty(refP, dexP, moveP), heaviest);
    }

    // ------------------------------------------------------------ skills

    private List<CharacterSkillLine> ComputeSkills(
        CharacterSheet sheet, Dictionary<string, int> effectiveStats, int actionPenalty, List<ActiveCyberwareEffect> effects)
    {
        var home = sheet.Identity.Home;
        var basic = rules.BasicSkillKeys(home).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roleSkills = sheet.PrimaryRole is { } r && rules.FindRole(r.Role) is not null
            ? rules.RoleSkillLevels(r.Role, home, sheet.Creation.SkillChoices).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roleIds = roleSkills.Select(k => SkillKey.Parse(k).Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lines = new List<CharacterSkillLine>();
        foreach (var def in rules.Skills)
        {
            var keys = def.Specialized
                ? sheet.Skills.Keys.Where(k => SkillKey.Parse(k).Id.Equals(def.Id, StringComparison.OrdinalIgnoreCase))
                    .Concat(basic.Where(k => SkillKey.Parse(k).Id.Equals(def.Id, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [def.Id];
            if (keys.Count == 0) keys = [def.Id];

            foreach (var key in keys)
            {
                var (_, spec) = SkillKey.Parse(key);
                var level = sheet.Skills.GetValueOrDefault(key);
                var statValue = effectiveStats.GetValueOrDefault(def.Stat);
                var modifierParts = sheet.Modifiers
                    .Where(m => m.Active && m.Target == ModifierTarget.Skill
                                && (m.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || m.Key.Equals(def.Id, StringComparison.OrdinalIgnoreCase)))
                    .Select(m => new ValuePart(m.Source, m.Value)).ToList();
                // Cyberware: an unconditional bonus joins the Base; a situational one ("for Checks which include sight") is only noted.
                var forSkill = effects.Where(a => a.Effect.Skills.Contains(def.Id, StringComparer.OrdinalIgnoreCase)).ToList();
                modifierParts.AddRange(forSkill.Where(a => a.Effect.Condition is null).Select(a => new ValuePart(a.Label, a.Bonus)));
                var situational = forSkill.Where(a => a.Effect.Condition is not null)
                    .Select(a => $"+{a.Bonus} {a.Effect.Condition} ({a.Label})").ToList();
                var modifier = modifierParts.Sum(p => p.Value);
                var baseValue = statValue + level + modifier;
                lines.Add(new CharacterSkillLine(
                    key, def.Id, def.Name, def.NamePt, spec, def.Stat, def.Category, level, statValue, modifier, baseValue,
                    actionPenalty, baseValue + actionPenalty, def.DoubleCost, def.Combat,
                    basic.Contains(key), roleSkills.Contains(key) || (spec is null && roleIds.Contains(def.Id)), def.Page,
                    level < rules.Document.Progression.MaxRank.Value ? rules.SkillImprovementCost(def, level + 1) : null,
                    modifierParts, situational));
            }
        }
        return lines;
    }

    // ------------------------------------------------------------ roles and injuries

    private List<RoleLine> ComputeRoles(CharacterSheet sheet) =>
        sheet.Roles.Select((r, i) =>
        {
            var info = rules.FindRole(r.Role)?.Ability;
            return new RoleLine(r.Role, r.Rank, info?.Name ?? "", info?.Page ?? 0, i == 0, r.IpInvested,
                r.Rank < rules.Document.Progression.MaxRank.Value ? rules.RoleImprovementCost(r.Rank + 1) : null);
        }).ToList();

    private sealed record ActiveInjury(CriticalInjuryRecord Record, CriticalInjuryDefinition? Definition);

    /// <summary>Injuries whose effect currently applies: Active only. A Quick Fix suspends the effect (CP:R p.222).</summary>
    private List<ActiveInjury> ActiveInjuries(CharacterSheet sheet) =>
        sheet.Injuries.Where(i => i.Status == InjuryStatus.Active)
            .Select(i => new ActiveInjury(i, rules.FindCriticalInjury(i.Location, i.Name)))
            .ToList();
}
