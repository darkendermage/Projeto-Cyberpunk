using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// Combat actions in the order of the Core rules (CP:R p.186–188). Nothing here advances on its own: the Death Save
/// Penalty only grows when a Death Save is recorded or a Mortally Wounded character takes damage, never because
/// time passed or the page was opened.
/// </summary>
public sealed class CombatOperations(CharacterRuleBook rules, CharacterCalculator calculator, IDiceRoller dice)
{
    private const string Cracked = "Cracked Skull";

    /// <summary>
    /// Takes an attack's damage: armor SP of the location is subtracted (unless the damage bypasses armor); what gets
    /// through is doubled on the head (tripled with Cracked Skull), lowers HP and ablates every armor on that location.
    /// </summary>
    public CharacterSheet Damage(CharacterSheet sheet, DamageRequest request, CharacterChange change)
    {
        if (request.Amount < 0) throw CharacterChange.Refuse("amount", "Dano não pode ser negativo.");
        var before = calculator.Calculate(sheet, change.Catalog);
        var slot = request.Location == InjuryLocation.Head ? ArmorSlot.Head : ArmorSlot.Body;
        var sp = DamageMath.EffectiveSp(slot == ArmorSlot.Head ? before.Armor.Head.Sp : before.Armor.Body.Sp, request.HalveArmor, request.BypassArmor);
        var cracked = sheet.Injuries.Any(i => i.Status == InjuryStatus.Active && i.Name.Equals(Cracked, StringComparison.OrdinalIgnoreCase));
        var through = DamageMath.Through(request.Amount, sp, slot == ArmorSlot.Head, cracked);

        var hp = before.Health.CurrentHp;
        var after = Math.Max(0, hp - through);
        var where = slot == ArmorSlot.Head ? "cabeça" : "corpo";
        change.Log(CharacterEventKind.Combat, "damage",
            (request.Description is { Length: > 0 } d ? d + ": " : "") +
            $"{request.Amount} de dano no {where}, SP {sp}{(request.BypassArmor ? " ignorado" : request.HalveArmor ? " (metade, corpo a corpo)" : "")}; {through} passaram.",
            -through, hp.ToString(), after.ToString());

        var combat = sheet.Combat with { CurrentHp = after, Stabilized = after >= 1 && sheet.Combat.Stabilized };
        if (before.Health.WoundState == WoundState.Mortally && through > 0)
        {
            // CP:R p.187: a Mortally Wounded character damaged by an Attack suffers a Critical Injury and +1 Death Save Penalty.
            var penalty = rules.Document.Derived.DeathSave.Value.PenaltyPerDamageWhileMortal;
            combat = combat with { DeathSavePenalty = combat.DeathSavePenalty + penalty };
            change.Log(CharacterEventKind.Combat, "death-save-penalty", $"Dano enquanto Mortally Wounded: penalidade de Death Save +{penalty}.", penalty);
            change.Notices.Add("Mortally Wounded e atingido: registre uma Critical Injury (CP:R p.187).");
        }
        sheet = sheet with { Combat = combat };

        if (through > 0 && !request.BypassArmor)
            sheet = Ablate(sheet, slot, rules.Document.Combat.AblationPerDamagingHit.Value, "dano que passou do SP", change);

        var now = calculator.WoundStateFor(after, before.Health.MaxHp);
        if (now != before.Health.WoundState)
            change.Notices.Add(now switch
            {
                WoundState.Seriously => "Seriously Wounded: -2 em todas as Ações (CP:R p.186).",
                WoundState.Mortally => "Mortally Wounded: -4 em todas as Ações, -6 em MOVE e Death Save no início de cada turno (CP:R p.186).",
                WoundState.Lightly => "Lightly Wounded: sem penalidade; estabilizar DV10 (CP:R p.186).",
                _ => "Sem ferimentos.",
            });
        return sheet;
    }

    /// <summary>
    /// Recovers HP (first aid, natural healing, Speedheal). A Mortally Wounded character must be stabilized first.
    /// </summary>
    public CharacterSheet Heal(CharacterSheet sheet, HealRequest request, CharacterChange change)
    {
        if (request.Amount <= 0) throw CharacterChange.Refuse("amount", "Informe quanto HP recuperar.");
        var report = calculator.Calculate(sheet, change.Catalog);
        if (report.Health.WoundState == WoundState.Mortally)
            throw CharacterChange.Refuse("amount", $"Mortally Wounded: estabilize primeiro (DV{report.Health.StabilizationDv}, CP:R p.186).");
        var after = Math.Min(report.Health.MaxHp, report.Health.CurrentHp + request.Amount);
        change.Log(CharacterEventKind.Combat, "heal",
            (request.Description is { Length: > 0 } d ? d + ": " : "") + $"recuperou {after - report.Health.CurrentHp} HP.",
            after - report.Health.CurrentHp, report.Health.CurrentHp.ToString(), after.ToString());
        return sheet with { Combat = sheet.Combat with { CurrentHp = after == report.Health.MaxHp ? null : after } };
    }

    /// <summary>
    /// Successful stabilization. A Mortally Wounded target goes to 1 HP, unconscious for a minute, and the Death Save
    /// Penalty returns to the base set by Critical Injuries (CP:R p.188, p.222).
    /// </summary>
    public CharacterSheet Stabilize(CharacterSheet sheet, CharacterChange change)
    {
        var report = calculator.Calculate(sheet, change.Catalog);
        var combat = sheet.Combat with { Stabilized = true };
        if (report.Health.WoundState == WoundState.Mortally)
        {
            combat = combat with
            {
                CurrentHp = 1,
                DeathSavePenalty = 0,
                Conditions = combat.Conditions.Append("Inconsciente").Distinct().ToList(),
            };
            change.Log(CharacterEventKind.Combat, "stabilize",
                "Estabilizado: 1 HP, inconsciente por 1 minuto; penalidade de Death Save volta à base.", 1 - report.Health.CurrentHp,
                report.Health.CurrentHp.ToString(), "1");
        }
        else
            change.Log(CharacterEventKind.Combat, "stabilize", "Estabilizado: a cura natural pode começar.");
        return sheet with { Combat = combat };
    }

    /// <summary>
    /// Death Save: 1d10 plus the current penalty must be under BODY, and a natural 10 always fails. Every Death Save
    /// raises the penalty by 1 (CP:R p.188). The roll can be entered from physical dice.
    /// </summary>
    public CharacterSheet DeathSave(CharacterSheet sheet, DeathSaveRequest request, CharacterChange change)
    {
        var report = calculator.Calculate(sheet, change.Catalog);
        if (!report.Health.MustRollDeathSave)
            throw CharacterChange.Refuse("roll", "Death Save só é rolado por quem está Mortally Wounded (CP:R p.188).");
        if (sheet.Combat.Conditions.Contains("Morto")) throw CharacterChange.Refuse("roll", "O personagem já falhou um Death Save.");
        var roll = request.Roll ?? dice.Roll(10);
        if (roll is < 1 or > 10) throw CharacterChange.Refuse("roll", "O d10 vai de 1 a 10.");

        var rule = rules.Document.Derived.DeathSave.Value;
        var penalty = report.Health.DeathSavePenalty;
        var total = roll + penalty;
        var success = !(rule.NaturalTenFails && roll == 10) && total < report.Health.DeathSave;
        var combat = sheet.Combat with { DeathSavePenalty = sheet.Combat.DeathSavePenalty + rule.PenaltyPerSave };
        if (!success) combat = combat with { Conditions = combat.Conditions.Append("Morto").Distinct().ToList() };

        change.Log(CharacterEventKind.Combat, "death-save",
            $"Death Save: d10 {roll} + {penalty} = {total} contra BODY {report.Health.DeathSave}: {(success ? "sobreviveu" : "falhou")}." +
            (roll == 10 ? " (10 natural sempre falha)" : ""),
            rule.PenaltyPerSave, penalty.ToString(), (penalty + rule.PenaltyPerSave).ToString(), manual: request.Roll is not null);
        change.Notices.Add(success ? $"Sobreviveu. Próximo Death Save com +{penalty + rule.PenaltyPerSave}." : "Falhou o Death Save: o personagem morreu (CP:R p.188).");
        return sheet with { Combat = combat };
    }

    // ------------------------------------------------------------ armor

    public CharacterSheet AblateManual(CharacterSheet sheet, AblateRequest request, CharacterChange change)
    {
        if (request.Amount < 1) throw CharacterChange.Refuse("amount", "Informe quanto SP reduzir.");
        return Ablate(sheet, request.Location, request.Amount, request.Description ?? "ablação manual", change);
    }

    /// <summary>All armor on the location is ablated together (CP:R p.184), worn pieces and armor implants alike.</summary>
    private CharacterSheet Ablate(CharacterSheet sheet, ArmorSlot slot, int amount, string reason, CharacterChange change)
    {
        var inventory = sheet.Inventory.Select(e =>
        {
            if (e.State != ItemState.Equipped || e.Worn != slot || change.Catalog.Find(e.ItemId)?.Armor is not { Type: ArmorType.BodyArmor, Sp: { } max } armor)
                return e;
            var current = e.CurrentSp ?? max;
            var next = Math.Max(0, current - amount);
            if (next != current)
                change.Log(CharacterEventKind.Combat, "ablation", $"{change.Item(e.ItemId).Name} ablada ({reason}).", next - current, current.ToString(), next.ToString());
            return e with { CurrentSp = next };
        }).ToList();

        var cyberware = sheet.Cyberware.Select(c =>
        {
            if (change.Catalog.Find(c.ItemId) is not { } item || rules.Shared.CyberwareStoppingPower(item.Slug) is not { } max) return c;
            var current = c.CurrentSp ?? max;
            var next = Math.Max(0, current - amount);
            if (next != current)
                change.Log(CharacterEventKind.Combat, "ablation", $"{item.Name} ablado ({reason}).", next - current, current.ToString(), next.ToString());
            return c with { CurrentSp = next };
        }).ToList();
        return sheet with { Inventory = inventory, Cyberware = cyberware };
    }

    public CharacterSheet RepairArmor(CharacterSheet sheet, RepairArmorRequest request, CharacterChange change)
    {
        if (sheet.Inventory.FirstOrDefault(e => e.InstanceId == request.InstanceId) is { } entry)
        {
            var max = change.Item(entry.ItemId).Armor?.Sp ?? throw CharacterChange.Refuse("instanceId", "Esse item não tem SP.");
            var sp = Bounded(request.Sp, max);
            change.Log(CharacterEventKind.Combat, "repair", $"{change.Item(entry.ItemId).Name} reparada.", sp - (entry.CurrentSp ?? max),
                (entry.CurrentSp ?? max).ToString(), sp.ToString());
            return sheet with { Inventory = sheet.Inventory.Select(e => e.InstanceId == entry.InstanceId ? e with { CurrentSp = sp == max ? null : sp } : e).ToList() };
        }
        var piece = sheet.Cyberware.FirstOrDefault(c => c.InstanceId == request.InstanceId)
                    ?? throw CharacterChange.Refuse("instanceId", "Armadura não encontrada.");
        var item = change.Item(piece.ItemId);
        var implantMax = rules.Shared.CyberwareStoppingPower(item.Slug) ?? throw CharacterChange.Refuse("instanceId", "Esse implante não tem SP.");
        var value = Bounded(request.Sp, implantMax);
        change.Log(CharacterEventKind.Combat, "repair", $"{item.Name} recuperado.", value - (piece.CurrentSp ?? implantMax),
            (piece.CurrentSp ?? implantMax).ToString(), value.ToString());
        return sheet with { Cyberware = sheet.Cyberware.Select(c => c.InstanceId == piece.InstanceId ? c with { CurrentSp = value == implantMax ? null : value } : c).ToList() };

        static int Bounded(int? sp, int max) => sp is { } v ? Math.Clamp(v, 0, max) : max;
    }

    // ------------------------------------------------------------ critical injuries

    /// <summary>
    /// Records a Critical Injury from the official tables. The target can't suffer the same injury twice at once
    /// (CP:R p.187); the 5 Bonus Damage goes straight to HP, without ablating armor.
    /// </summary>
    public CharacterSheet AddInjury(CharacterSheet sheet, AddInjuryRequest request, CharacterChange change)
    {
        var def = rules.FindCriticalInjury(request.Location, request.Name)
                  ?? throw CharacterChange.Refuse("name", $"{request.Name} não está na tabela de Critical Injuries {(request.Location == InjuryLocation.Head ? "da cabeça" : "do corpo")}.");
        if (sheet.Injuries.Any(i => i.Status != InjuryStatus.Healed && i.Location == request.Location && i.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase)))
            throw CharacterChange.Refuse("name", $"O personagem já sofre de {def.Name}; role de novo na tabela (CP:R p.187).");

        change.Sheet = sheet;
        var record = new CriticalInjuryRecord
        {
            Id = change.NewId("inj"), Name = def.Name, Location = request.Location, Detail = request.Detail, ReceivedAt = change.Now,
        };
        change.Log(CharacterEventKind.Injury, "critical-injury", $"Critical Injury: {def.Name}. {def.Effect}");
        sheet = sheet with { Injuries = [.. sheet.Injuries, record] };

        if (request.ApplyBonusDamage)
        {
            var bonus = rules.Document.Combat.CriticalInjury.Value.BonusDamage;
            var report = calculator.Calculate(sheet, change.Catalog);
            var after = Math.Max(0, report.Health.CurrentHp - bonus);
            change.Log(CharacterEventKind.Combat, "critical-bonus-damage", $"Dano bônus de {def.Name}, direto no HP ({CharacterRuleBook.Page(rules.Document.Combat.CriticalInjury.Reference)}).",
                after - report.Health.CurrentHp, report.Health.CurrentHp.ToString(), after.ToString());
            sheet = sheet with { Combat = sheet.Combat with { CurrentHp = after, Stabilized = after >= 1 && sheet.Combat.Stabilized } };
        }
        return sheet;
    }

    /// <summary>
    /// Quick Fix suspends the effect for the rest of the day, or removes it for good when the table says so; Treatment
    /// removes it permanently (CP:R p.222). An injury without Quick Fix ("N/A") can only be treated.
    /// </summary>
    public CharacterSheet UpdateInjury(CharacterSheet sheet, string id, UpdateInjuryRequest request, CharacterChange change)
    {
        var record = sheet.Injuries.FirstOrDefault(i => i.Id == id) ?? throw CharacterChange.Refuse("id", "Lesão não encontrada.");
        var def = rules.FindCriticalInjury(record.Location, record.Name);
        if (record.Status == InjuryStatus.Healed) throw CharacterChange.Refuse("status", "Essa lesão já foi tratada.");

        var updated = request.Status switch
        {
            InjuryStatus.QuickFixed when def?.QuickFix is null =>
                throw CharacterChange.Refuse("status", $"{record.Name} não tem Quick Fix; só Treatment ({def?.Treatment})."),
            InjuryStatus.QuickFixed when def!.QuickFixHeals =>
                record with { Status = InjuryStatus.Healed, QuickFixedAt = change.Now, ResolvedAt = change.Now, QuickFixNote = request.Note },
            InjuryStatus.QuickFixed => record with { Status = InjuryStatus.QuickFixed, QuickFixedAt = change.Now, QuickFixNote = request.Note },
            InjuryStatus.Healed => record with { Status = InjuryStatus.Healed, ResolvedAt = change.Now, TreatmentNote = request.Note },
            _ => record with { Status = InjuryStatus.Active },
        };
        change.Log(CharacterEventKind.Injury, "injury-status", updated.Status switch
        {
            InjuryStatus.Healed when request.Status == InjuryStatus.QuickFixed => $"{record.Name}: o Quick Fix removeu o efeito de vez.",
            InjuryStatus.Healed => $"{record.Name}: tratada ({def?.Treatment}).",
            InjuryStatus.QuickFixed => $"{record.Name}: Quick Fix ({def?.QuickFix}); efeito suspenso pelo resto do dia.",
            _ => $"{record.Name}: efeito ativo de novo.",
        }, manual: true);
        return sheet with { Injuries = sheet.Injuries.Select(i => i.Id == id ? updated : i).ToList() };
    }

    /// <summary>Removes an injury entered by mistake (logged as a manual change).</summary>
    public static CharacterSheet RemoveInjury(CharacterSheet sheet, string id, CharacterChange change)
    {
        var record = sheet.Injuries.FirstOrDefault(i => i.Id == id) ?? throw CharacterChange.Refuse("id", "Lesão não encontrada.");
        change.Log(CharacterEventKind.Injury, "injury-removed", $"{record.Name} removida do registro (lançamento corrigido).", manual: true);
        return sheet with { Injuries = sheet.Injuries.Where(i => i.Id != id).ToList() };
    }

    public static CharacterSheet ClearConditions(CharacterSheet sheet, CharacterChange change)
    {
        if (sheet.Combat.Conditions.Count == 0) return sheet;
        change.Log(CharacterEventKind.Combat, "conditions-cleared", $"Condições removidas: {string.Join(", ", sheet.Combat.Conditions)}.", manual: true);
        return sheet with { Combat = sheet.Combat with { Conditions = [] } };
    }
}
