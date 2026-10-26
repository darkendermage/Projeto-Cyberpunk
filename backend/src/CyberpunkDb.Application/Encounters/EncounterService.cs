using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Encounters;

/// <summary>
/// The combat table (Friday Night Firefight). NPCs and generic enemies live in the encounter; player characters live on
/// their sheets, so every hit on a PC goes through the sheet's own combat operations. Each write carries the
/// encounter version (409 on a stale one).
/// </summary>
public sealed class EncounterService(
    IAppDbContext db,
    CombatRuleBook combat,
    CharacterRuleBook characterRules,
    CharacterService characters,
    NpcService npcs,
    NpcCatalogProvider catalogs,
    FighterViews views,
    AttackResolver resolver,
    NpcCombat npcCombat,
    IDiceRoller dice)
{
    internal static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const int MaxCount = 12;

    public EncounterRulesDto GetRules() =>
        new(combat.Document, new { characterRules.Document.Combat, characterRules.Document.Derived });

    // ------------------------------------------------------------------ library

    public async Task<IReadOnlyList<EncounterSummaryDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Encounters.AsNoTracking().OrderByDescending(e => e.UpdatedAt).ToListAsync(ct);
        return rows.Select(e =>
        {
            var s = Deserialize(e.StateJson);
            return new EncounterSummaryDto(e.Id, e.Name, e.Status, e.Round,
                s.Combatants.Count(c => c.Side == CombatSide.Ally), s.Combatants.Count(c => c.Side == CombatSide.Enemy),
                s.Combatants.Count(c => c.Dead || (c.Kind != CombatantKind.Pc && c.Hp <= 0)), e.UpdatedAt);
        }).ToList();
    }

    public async Task<EncounterDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        return await DetailAsync(entity, Deserialize(entity.StateJson), null, [], ct);
    }

    public async Task<EncounterDetailDto> CreateAsync(CreateEncounterRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entity = new Encounter
        {
            Name = Truncate(string.IsNullOrWhiteSpace(request.Name) ? $"Tiroteio {now:dd/MM HH:mm}" : request.Name.Trim(), 80),
            CreatedAt = now, Status = EncounterStatus.Active,
        };
        Apply(entity, new EncounterState(), now);
        db.Encounters.Add(entity);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(entity, new EncounterState(), null, [], ct);
    }

    public Task<EncounterDetailDto> UpdateAsync(int id, UpdateEncounterRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw RequestValidationException.For("name", L.T("Dê um nome ao encontro.", "Give the encounter a name."));
        return MutateAsync(id, r.Version, (s, e, _) =>
        {
            e.Name = Truncate(r.Name.Trim(), 80);
            return Task.FromResult(s with { Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : Truncate(r.Notes.Trim(), 4000) });
        }, ct);
    }

    public Task<EncounterDetailDto> EndAsync(int id, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, e, _) =>
        {
            e.Status = e.Status == EncounterStatus.Active ? EncounterStatus.Ended : EncounterStatus.Active;
            return Task.FromResult(Log(s, null, "end", e.Status == EncounterStatus.Ended ? L.T("Combate encerrado.", "Fight ended.") : L.T("Combate reaberto.", "Fight reopened.")));
        }, ct);

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        db.Encounters.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ combatants

    public Task<EncounterDetailDto> AddCombatantsAsync(int id, AddCombatantsRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var count = Math.Clamp(r.Count, 1, MaxCount);
            var added = new List<Combatant>();
            if (r.CharacterId is { } characterId)
            {
                if (s.Combatants.Any(c => c.CharacterId == characterId))
                    throw RequestValidationException.For("characterId", L.T("Esse personagem já está no combate.", "That character is already in the fight."));
                var pc = await characters.GetAsync(characterId, ct);
                added.Add(new Combatant
                {
                    Id = NextId(s, added), Kind = CombatantKind.Pc, Side = r.Side, CharacterId = characterId,
                    Name = pc.Sheet.Identity.Handle is { Length: > 0 } h ? h : pc.Sheet.Identity.Name,
                });
            }
            else if (r.NpcId is { } npcId)
            {
                var npc = await npcs.GetAsync(npcId, ct);
                var snapshot = views.Snapshot(npc.Sheet, npc.Report, await catalogs.GetAsync(ct), npcId, $"NPC salvo: {npc.Sheet.Name}");
                for (var i = 0; i < count; i++)
                    added.Add(Npc(s, added, NameOf(r.Name ?? npc.Sheet.Name, i, count), r.Side, CombatantKind.Npc, snapshot));
            }
            else if (r.Generate is { } generate)
            {
                var catalog = await catalogs.GetAsync(ct);
                for (var i = 0; i < count; i++)
                {
                    var result = await npcs.GenerateAsync(generate with { Seed = generate.Seed is { } seed ? seed + i : null }, ct);
                    if (!result.Success || result.Sheet is null || result.Report is null)
                        throw RequestValidationException.For("generate", string.Join(" ", result.Conflicts.Select(c => c.Message)));
                    var sheet = result.Sheet;
                    var name = r.Name is { Length: > 0 } n ? NameOf(n, i, count) : $"{sheet.Name} ({sheet.Concept})";
                    added.Add(Npc(s, added, name, r.Side, CombatantKind.Npc, views.Snapshot(sheet, result.Report, catalog, null, $"Gerado: {sheet.Concept}, seed {result.Seed}")));
                }
            }
            else if (r.Generic is { } generic)
            {
                var snapshot = views.Generic(generic);
                var baseName = string.IsNullOrWhiteSpace(generic.Name) ? "Inimigo" : generic.Name.Trim();
                for (var i = 0; i < count; i++) added.Add(Npc(s, added, NameOf(baseName, i, count), r.Side, CombatantKind.Generic, snapshot));
            }
            else throw RequestValidationException.For("combatant", L.T("Escolha um personagem, um NPC salvo, um gerado ou um genérico.", "Pick a character, a saved NPC, a generated one or a generic one."));

            notices.Add(L.T($"{added.Count} combatente(s) na mesa.", $"{added.Count} combatant(s) at the table."));
            return Log(s with { Combatants = [.. s.Combatants, .. added] }, null, "join", $"Entraram: {string.Join(", ", added.Select(a => a.Name))}.");
        }, ct);

    public Task<EncounterDetailDto> RemoveCombatantAsync(int id, string combatantId, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var c = Require(s, combatantId);
            var next = s with { Combatants = s.Combatants.Where(x => x.Id != combatantId).ToList(), ActiveId = s.ActiveId == combatantId ? null : s.ActiveId };
            return Task.FromResult(Log(next, null, "leave", L.T($"{c.Name} saiu do combate.", $"{c.Name} left the fight.")));
        }, ct);

    // ------------------------------------------------------------------ initiative and turns

    /// <summary>REF + 1d10 for everyone (or the listed ones); ties are broken by rolling again (CP:R p.168).</summary>
    public Task<EncounterDetailDto> RollInitiativeAsync(int id, InitiativeRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var fighters = await ViewsAsync(s, ct);
            var who = r.Only is { Count: > 0 } only ? only.ToHashSet() : s.Combatants.Select(c => c.Id).ToHashSet();
            var lines = new List<string>();
            var list = s.Combatants.Select(c =>
            {
                if (!who.Contains(c.Id)) return c;
                var roll = r.Rolls?.GetValueOrDefault(c.Id) is int typed and >= 1 and <= 10 ? typed : dice.Roll(10);
                var reflex = fighters[c.Id].Ref;
                lines.Add($"{c.Name}: REF {reflex} + {roll} = {reflex + roll}");
                return c with { Initiative = reflex + roll, TieBreak = 0 };
            }).ToList();
            list = BreakTies(list);
            var next = s with { Combatants = list };
            next = next with { ActiveId = s.ActiveId ?? next.Queue.FirstOrDefault()?.Id };
            notices.Add(L.T("Iniciativa rolada.", "Initiative rolled."));
            return Log(next, null, "initiative", $"Iniciativa ({combat.Document.Initiative.Source}): {string.Join("; ", lines)}.");
        }, ct);

    public Task<EncounterDetailDto> SetInitiativeAsync(int id, string combatantId, SetInitiativeRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var c = Require(s, combatantId);
            var next = Replace(s, c with { Initiative = r.Initiative, TieBreak = 0 });
            next = next with { Combatants = BreakTies(next.Combatants) };
            return Task.FromResult(Log(next, c.Id, "initiative", L.T($"{c.Name}: Iniciativa {r.Initiative} (ajuste manual).", $"{c.Name}: Initiative {r.Initiative} (manual adjustment).")));
        }, ct);

    /// <summary>
    /// Manual reorder: the moved combatant takes the Initiative of its new neighbor below (above when at the bottom) and the
    /// tie with that Initiative is settled in the order the GM chose, so the queue stays sorted by Initiative.
    /// </summary>
    public Task<EncounterDetailDto> MoveInQueueAsync(int id, string combatantId, MoveInQueueRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var c = Require(s, combatantId);
            if (r.BeforeId == c.Id) return Task.FromResult(s);
            var order = s.Queue.Where(x => x.Id != c.Id).ToList();
            var at = r.BeforeId is null ? order.Count : order.FindIndex(x => x.Id == r.BeforeId);
            if (at < 0) throw new NotFoundException(L.T("Combatente não encontrado.", "Combatant not found."));
            order.Insert(at, c);

            var initiative = (at + 1 < order.Count ? order[at + 1].Initiative : null) ?? (at > 0 ? order[at - 1].Initiative : null)
                ?? throw RequestValidationException.For("initiative", L.T("Role a iniciativa primeiro.", "Roll initiative first."));
            order[at] = c with { Initiative = initiative };
            var group = order.Where(x => x.Initiative == initiative).ToList();
            var tieBreaks = group.Select((x, i) => (x.Id, TieBreak: group.Count == 1 ? 0 : group.Count - i)).ToDictionary(x => x.Id, x => x.TieBreak);
            var list = s.Combatants.Select(x => x.Id == c.Id ? order[at] : x)
                .Select(x => tieBreaks.TryGetValue(x.Id, out var t) ? x with { TieBreak = t } : x).ToList();
            var next = s with { Combatants = list };
            return Task.FromResult(Log(next, c.Id, "initiative", L.T($"{c.Name}: movido na fila (Iniciativa {initiative}, ajuste manual).", $"{c.Name}: moved in the queue (Initiative {initiative}, manual adjustment).")));
        }, ct);

    public Task<EncounterDetailDto> SetActiveAsync(int id, string combatantId, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) => Task.FromResult(s with { ActiveId = Require(s, combatantId).Id }), ct);

    /// <summary>
    /// Ends the current Turn (fire burns at the end of it, CP:R p.180) and passes it to the next one in the Initiative
    /// Queue, skipping the dead; past the bottom a new Round starts (CP:R p.168).
    /// </summary>
    public Task<EncounterDetailDto> NextTurnAsync(int id, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, entity, notices) =>
        {
            if (s.Combatants.All(c => c.Initiative is null)) throw RequestValidationException.For("initiative", L.T("Role a iniciativa primeiro.", "Roll initiative first."));
            if (s.Find(s.ActiveId) is { } current)
                foreach (var burning in current.Conditions.Where(x => x.StartsWith("OnFire:")).ToList())
                    s = await EnvironmentOnAsync(s, current.Id, burning["OnFire:".Length..], null, notices, ct);

            var fighters = await ViewsAsync(s, ct);
            var queue = s.Queue.Where(c => c.Initiative is not null).ToList();
            var index = queue.FindIndex(c => c.Id == s.ActiveId);
            var round = s.Round;
            for (var step = 0; step < queue.Count; step++)
            {
                index++;
                if (index >= queue.Count) { index = 0; round++; }
                if (!fighters[queue[index].Id].Dead) break;
            }
            var actor = queue[index];
            var next = s with { ActiveId = actor.Id, Round = round };
            if (round != s.Round) next = Log(next, null, "round", $"Rodada {round}.");
            var view = fighters[actor.Id];
            if (view.MustDeathSave) notices.Add(L.T($"{actor.Name} está Mortally Wounded: role o Death Save antes do turno (CP:R p.188).", $"{actor.Name} is Mortally Wounded: roll the Death Save before the turn (CP:R p.188)."));
            if (actor.Conditions.Contains("Prone")) notices.Add(L.T($"{actor.Name} está Prone: precisa do Get Up antes de mover (CP:R p.169).", $"{actor.Name} is Prone: needs a Get Up before moving (CP:R p.169)."));
            entity.Round = round;
            return Log(next, actor.Id, "turn", L.T($"Turno de {actor.Name}.", $"{actor.Name}'s turn."));
        }, ct);

    // ------------------------------------------------------------------ attacks

    public Task<EncounterDetailDto> AttackAsync(int id, AttackRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var fighters = await ViewsAsync(s, ct);
            var attacker = fighters.GetValueOrDefault(r.AttackerId) ?? throw new NotFoundException(L.T("Atacante não está no combate.", "The attacker isn't in the fight."));
            if (attacker.Dead) throw RequestValidationException.For("attackerId", L.T($"{attacker.Name} está morto.", $"{attacker.Name} is dead."));
            var targets = r.TargetIds.Distinct().Select(t => fighters.GetValueOrDefault(t) ?? throw new NotFoundException(L.T("Alvo não está no combate.", "The target isn't in the fight."))).ToList();
            var plan = resolver.Resolve(r, attacker, targets, dice);

            // Ammunition first: if the magazine can't pay, nothing happens.
            s = await SpendAmmoAsync(s, attacker, plan, r.Mode, ct);

            var results = new List<TargetResult>();
            foreach (var hit in plan.Hits)
            {
                var (state, result) = await ApplyHitAsync(s, fighters[hit.TargetId], hit, r, notices, ct);
                s = state;
                results.Add(result with { Attack = plan.AttackTotal });
            }
            var attack = new AttackResult(attacker.Name, plan.WeaponName, plan.Mode, plan.AttackRoll, plan.AttackTotal, results, plan.Notes);
            _lastAttack = attack;
            var summary = plan.Mode == AttackMode.Suppressive
                ? string.Join("; ", results.Select(t => $"{t.TargetName}: {t.Notes.FirstOrDefault()}"))
                : string.Join("; ", results.Select(t => t.Hit ? L.T($"{t.TargetName}: acertou, {t.Damage} de dano{(t.Through is { } th ? $", {th} no HP" : "")}{(t.CriticalInjury is { } ci ? $", {ci}" : "")}", $"{t.TargetName}: hit, {t.Damage} damage{(t.Through is { } th2 ? $", {th2} to HP" : "")}{(t.CriticalInjury is { } ci2 ? $", {ci2}" : "")}") : $"{t.TargetName}: errou"));
            return Log(s, attacker.Id, "attack",
                L.T($"{attacker.Name} ataca com {plan.WeaponName} ({ModeLabel(plan.Mode)}): d10 {plan.AttackRoll} → {plan.AttackTotal}. {summary}.", $"{attacker.Name} attacks with {plan.WeaponName} ({ModeLabel(plan.Mode)}): d10 {plan.AttackRoll} → {plan.AttackTotal}. {summary}."));
        }, ct);

    private AttackResult? _lastAttack;

    private async Task<EncounterState> SpendAmmoAsync(EncounterState s, FighterView attacker, PlannedAttack plan, AttackMode mode, CancellationToken ct)
    {
        var weapon = attacker.Weapon(plan.WeaponKey);
        if (weapon is null || weapon.Magazine is null || weapon.Melee) return s;
        if (attacker.Kind == CombatantKind.Pc)
        {
            var sheet = await characters.GetAsync(attacker.CharacterId!.Value, ct);
            var fireMode = mode switch { AttackMode.Autofire => FireMode.Autofire, AttackMode.Suppressive => FireMode.Suppressive, _ => FireMode.Single };
            await characters.FireAsync(attacker.CharacterId.Value, new FireRequest(sheet.Version, weapon.Key, fireMode, plan.Rounds), ct);
            return s;
        }
        var c = Require(s, attacker.Id);
        var left = c.Ammo.GetValueOrDefault(weapon.Key, weapon.Magazine.Value);
        if (left < plan.Rounds) throw RequestValidationException.For("weapon", L.T($"{weapon.Name} tem {left} bala(s); recarregue.", $"{weapon.Name} has {left} round(s); reload."));
        return Replace(s, c with { Ammo = new Dictionary<string, int>(c.Ammo) { [weapon.Key] = left - plan.Rounds } });
    }

    private async Task<(EncounterState, TargetResult)> ApplyHitAsync(EncounterState s, FighterView target, PlannedHit hit, AttackRequest r,
        List<string> notices, CancellationToken ct)
    {
        var notes = new List<string>(hit.Notes);
        string Loc() => hit.Location == InjuryLocation.Head ? L.T("cabeça", "head") : "corpo";
        if (!hit.Hit || hit.Damage <= 0 && !hit.Critical)
            return (s, new TargetResult(target.Id, target.Name, hit.Hit, 0, hit.Dv, hit.DefenseRoll, hit.Defense, hit.Dice, 0, Loc(), null, false,
                null, null, false, null, null, notes));

        var amount = hit.Damage;
        int? absorbed = null;
        var destroyed = false;
        if (!r.IgnoreCover && target.CoverId is { } coverId && s.Covers.FirstOrDefault(c => c.Id == coverId) is { Hp: > 0 } cover)
        {
            // CP:R p.182: cover takes the hit; excess is lost. CP:R p.174: a blast that destroys the cover hits in full.
            var left = cover.Hp - amount;
            destroyed = left <= 0;
            absorbed = Math.Min(cover.Hp, amount);
            s = ReplaceCover(s, cover with { Hp = Math.Max(0, left) });
            if (destroyed) s = s with { Combatants = s.Combatants.Select(c => c.CoverId == cover.Id ? c with { CoverId = null } : c).ToList() };
            notes.Add(destroyed ? L.T($"{cover.Label} foi destruída.", $"{L.D(cover.Label)} was destroyed.") : $"{cover.Label} absorveu o golpe ({Math.Max(0, left)} HP restantes).");
            if (!(hit.Explosive && destroyed))
                return (s, new TargetResult(target.Id, target.Name, true, 0, hit.Dv, hit.DefenseRoll, hit.Defense, hit.Dice, amount, Loc(), absorbed, destroyed,
                    null, 0, false, null, target.WoundState.ToString(), notes));
        }

        var (next, sp, through, after, crit) = await DamageAsync(s, target, amount, hit.Location, hit.HalveArmor, false, hit.Critical, r.CritRoll,
            $"{ModeLabel(hit.Mode)}", fromAttack: true, notes, notices, ct);
        s = next;

        // CP:R p.170: aimed shots at a hand or a leg.
        if (hit.Mode == AttackMode.AimedLeg && through > 0)
            (s, crit) = await AddNamedCriticalAsync(s, target, "Broken Leg", notes, ct, crit);
        if (hit.Mode == AttackMode.AimedHand && through > 0) notes.Add(L.T("O alvo derruba um item da mão, à escolha do atacante (CP:R p.170).", "The target drops a held item, of the attacker's choice (CP:R p.170)."));

        return (s, new TargetResult(target.Id, target.Name, true, 0, hit.Dv, hit.DefenseRoll, hit.Defense, hit.Dice, amount, Loc(), absorbed, destroyed,
            sp, through, crit is not null, crit, after.ToString(), notes));
    }

    // ------------------------------------------------------------------ damage, healing, Death Saves

    public Task<EncounterDetailDto> DamageCombatantAsync(int id, string combatantId, ManualDamageRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            if (r.Amount < 0) throw RequestValidationException.For("amount", L.T("Dano não pode ser negativo.", "Damage can't be negative."));
            var fighters = await ViewsAsync(s, ct);
            var target = fighters.GetValueOrDefault(combatantId) ?? throw new NotFoundException(L.T("Combatente não encontrado.", "Combatant not found."));
            var notes = new List<string>();
            var (next, _, through, after, crit) = await DamageAsync(s, target, r.Amount, r.Location, r.HalveArmor, r.BypassArmor, r.Critical, null,
                r.Description ?? L.T("dano manual", "manual damage"), fromAttack: r.Critical, notes, notices, ct);
            return Log(next, target.Id, "damage", L.T($"{target.Name}: {r.Amount} de dano ({(r.Location == InjuryLocation.Head ? "cabeça" : "corpo")}), {through} no HP", $"{target.Name}: {r.Amount} damage ({(r.Location == InjuryLocation.Head ? "head" : "body")}), {through} to HP")
                + (crit is not null ? $", {crit}" : "") + $". {Translate(after)}.");
        }, ct);

    /// <summary>Fire, electrocution, poison, falls (CP:R p.180-181). Environmental damage never causes Critical Injuries.</summary>
    public Task<EncounterDetailDto> EnvironmentAsync(int id, string combatantId, EnvironmentDamageRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, notices) => EnvironmentOnAsync(s, combatantId, r.EnvironmentId, r.Dice, notices, ct), ct);

    private async Task<EncounterState> EnvironmentOnAsync(EncounterState s, string combatantId, string environmentId, List<int>? typed,
        List<string> notices, CancellationToken ct)
    {
        var env = combat.Document.Environment.Value.FirstOrDefault(e => e.Id == environmentId)
                  ?? throw RequestValidationException.For("environmentId", L.T($"'{environmentId}' não existe.", $"'{environmentId}' doesn't exist."));
        var fighters = await ViewsAsync(s, ct);
        var target = fighters.GetValueOrDefault(combatantId) ?? throw new NotFoundException(L.T("Combatente não encontrado.", "Combatant not found."));
        var rolled = AttackResolver.RollDice(env.Damage, typed, dice);
        var (next, _, through, after, _) = await DamageAsync(s, target, rolled.Sum(), InjuryLocation.Body, false, env.BypassArmor, false, null,
            env.Label, fromAttack: false, [], notices, ct);
        return Log(next, target.Id, "environment", L.T($"{target.Name}: {env.Label}, {rolled.Sum()} de dano, {through} no HP. {Translate(after)}.", $"{target.Name}: {L.D(env.Label)}, {rolled.Sum()} damage, {through} to HP. {Translate(after)}."));
    }

    /// <summary>Applies damage to a PC (through its sheet) or to an NPC (in the encounter), with the Critical Injury if any.</summary>
    private async Task<(EncounterState State, int? Sp, int Through, WoundState After, string? Critical)> DamageAsync(
        EncounterState s, FighterView target, int amount, InjuryLocation location, bool halve, bool bypass, bool critical, int? critRoll,
        string description, bool fromAttack, List<string> notes, List<string> notices, CancellationToken ct)
    {
        if (target.Kind == CombatantKind.Pc)
        {
            var id = target.CharacterId!.Value;
            var before = await characters.GetAsync(id, ct);
            var wasMortal = before.Report.Health.WoundState == WoundState.Mortally;
            var sp = DamageMath.EffectiveSp(location == InjuryLocation.Head ? before.Report.Armor.Head.Sp : before.Report.Armor.Body.Sp, halve, bypass);
            var after = await characters.DamageAsync(id, new DamageRequest(before.Version, amount, location, bypass, description, halve), ct);
            var through = before.Report.Health.CurrentHp - after.Report.Health.CurrentHp;
            string? crit = null;
            if (critical || (fromAttack && wasMortal && through > 0))
            {
                var active = after.Sheet.Injuries.Where(i => i.Status != InjuryStatus.Healed && i.Location == location).Select(i => i.Name);
                var def = npcCombat.RollCritical(location, active, critRoll, dice);
                after = await characters.AddInjuryAsync(id, new AddInjuryRequest(after.Version, location, def.Name), ct);
                crit = def.Name;
                notes.Add(L.T($"Critical Injury: {def.Name}. {def.Effect} (+{characterRules.Document.Combat.CriticalInjury.Value.BonusDamage} no HP)", $"Critical Injury: {def.Name}. {L.D(def.Effect)} (+{characterRules.Document.Combat.CriticalInjury.Value.BonusDamage} to HP)"));
            }
            notices.AddRange(after.Notices);
            return (s, sp, through, after.Report.Health.WoundState, crit);
        }

        var c = Require(s, target.Id);
        var outcome = npcCombat.Damage(c, amount, location, halve, bypass, fromAttack);
        notes.AddRange(outcome.Notes);
        var updated = outcome.Combatant;
        string? critName = null;
        if (critical || (fromAttack && outcome.Before == WoundState.Mortally && outcome.Through > 0))
        {
            var def = npcCombat.RollCritical(location, updated.Injuries.Where(i => i.Location == location).Select(i => i.Name), critRoll, dice);
            updated = npcCombat.AddCritical(updated, def, location);
            critName = def.Name;
            notes.Add(L.T($"Critical Injury: {def.Name}. {def.Effect} (+{characterRules.Document.Combat.CriticalInjury.Value.BonusDamage} no HP)", $"Critical Injury: {def.Name}. {L.D(def.Effect)} (+{characterRules.Document.Combat.CriticalInjury.Value.BonusDamage} to HP)"));
        }
        var after2 = npcCombat.Wound(updated);
        if (after2 != outcome.Before) notices.Add($"{c.Name}: {Translate(after2)}.");
        return (Replace(s, updated), outcome.Sp, outcome.Through, after2, critName);
    }

    private async Task<(EncounterState, string?)> AddNamedCriticalAsync(EncounterState s, FighterView target, string name, List<string> notes,
        CancellationToken ct, string? already)
    {
        var def = characterRules.FindCriticalInjury(InjuryLocation.Body, name)!;
        if (target.Kind == CombatantKind.Pc)
        {
            var sheet = await characters.GetAsync(target.CharacterId!.Value, ct);
            if (sheet.Sheet.Injuries.Any(i => i.Status != InjuryStatus.Healed && i.Name == name)) return (s, already);
            await characters.AddInjuryAsync(target.CharacterId.Value, new AddInjuryRequest(sheet.Version, InjuryLocation.Body, name), ct);
        }
        else
        {
            var c = Require(s, target.Id);
            if (c.Injuries.Any(i => i.Name == name)) return (s, already);
            s = Replace(s, npcCombat.AddCritical(c, def, InjuryLocation.Body));
        }
        notes.Add(L.T($"Tiro na perna: {name}. {def.Effect}", $"Shot to the leg: {name}. {L.D(def.Effect)}"));
        return (s, already is null ? name : $"{already}, {name}");
    }

    public Task<EncounterDetailDto> HealAsync(int id, string combatantId, HealCombatantRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var c = Require(s, combatantId);
            if (c.Kind == CombatantKind.Pc)
            {
                var sheet = await characters.GetAsync(c.CharacterId!.Value, ct);
                notices.AddRange((await characters.HealAsync(c.CharacterId.Value, new HealRequest(sheet.Version, r.Amount, L.T("Mesa de combate", "Combat table")), ct)).Notices);
                return Log(s, c.Id, "heal", $"{c.Name} recuperou {r.Amount} HP.");
            }
            return Log(Replace(s, npcCombat.Heal(c, r.Amount)), c.Id, "heal", $"{c.Name} recuperou {r.Amount} HP.");
        }, ct);

    public Task<EncounterDetailDto> StabilizeAsync(int id, string combatantId, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var c = Require(s, combatantId);
            if (c.Kind == CombatantKind.Pc)
            {
                var sheet = await characters.GetAsync(c.CharacterId!.Value, ct);
                notices.AddRange((await characters.StabilizeAsync(c.CharacterId.Value, new VersionRequest(sheet.Version), ct)).Notices);
            }
            else s = Replace(s, npcCombat.Stabilize(c));
            return Log(s, c.Id, "stabilize", L.T($"{c.Name} foi estabilizado.", $"{c.Name} was stabilized."));
        }, ct);

    public Task<EncounterDetailDto> DeathSaveAsync(int id, string combatantId, DeathSaveCombatantRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, async (s, _, notices) =>
        {
            var c = Require(s, combatantId);
            if (c.Kind == CombatantKind.Pc)
            {
                var sheet = await characters.GetAsync(c.CharacterId!.Value, ct);
                var after = await characters.DeathSaveAsync(c.CharacterId.Value, new DeathSaveRequest(sheet.Version, r.Roll), ct);
                notices.AddRange(after.Notices);
                return Log(s, c.Id, "death-save", $"{c.Name}: {string.Join(" ", after.Notices)}");
            }
            var roll = r.Roll ?? dice.Roll(10);
            if (roll is < 1 or > 10) throw RequestValidationException.For("roll", "O d10 vai de 1 a 10.");
            var (next, survived, text) = npcCombat.DeathSave(c, roll);
            notices.Add($"{c.Name}: {(survived ? "sobreviveu" : "morreu")}.");
            return Log(Replace(s, next), c.Id, "death-save", $"{c.Name}: {text}");
        }, ct);

    public Task<EncounterDetailDto> ConditionAsync(int id, string combatantId, ConditionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var c = Require(s, combatantId);
            if (string.IsNullOrWhiteSpace(r.Condition)) throw RequestValidationException.For("condition", L.T("Informe a condição.", "Enter the condition."));
            var condition = r.Condition.Trim();
            var list = r.On ? c.Conditions.Where(x => !x.StartsWith("OnFire:") || !condition.StartsWith("OnFire:")).Append(condition).Distinct().ToList()
                            : c.Conditions.Where(x => x != condition).ToList();
            return Task.FromResult(Log(Replace(s, c with { Conditions = list }), c.Id, "condition", $"{c.Name}: {(r.On ? "+" : "−")} {condition}."));
        }, ct);

    // ------------------------------------------------------------------ cover

    public Task<EncounterDetailDto> AddCoverAsync(int id, CoverRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var hp = r.Hp ?? combat.CoverHp(r.Material, r.Thick);
            if (hp <= 0) throw RequestValidationException.For("material", L.T($"Isso não para bala: não é cobertura ({combat.Document.CoverRules.Source}).", $"That doesn't stop bullets: it isn't cover ({combat.Document.CoverRules.Source})."));
            var cover = new CoverPiece
            {
                Id = $"K{Enumerable.Range(1, 999).First(n => s.Covers.All(c => c.Id != $"K{n}"))}",
                Label = string.IsNullOrWhiteSpace(r.Label) ? "Cobertura" : Truncate(r.Label.Trim(), 60), Material = r.Material, Thick = r.Thick,
                MaxHp = hp, Hp = hp,
            };
            return Task.FromResult(Log(s with { Covers = [.. s.Covers, cover] }, null, "cover", $"Cobertura: {cover.Label} ({hp} HP)."));
        }, ct);

    public Task<EncounterDetailDto> RemoveCoverAsync(int id, string coverId, EncounterVersionRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) => Task.FromResult(s with
        {
            Covers = s.Covers.Where(c => c.Id != coverId).ToList(),
            Combatants = s.Combatants.Select(c => c.CoverId == coverId ? c with { CoverId = null } : c).ToList(),
        }), ct);

    public Task<EncounterDetailDto> DamageCoverAsync(int id, string coverId, CoverDamageRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var cover = s.Covers.FirstOrDefault(c => c.Id == coverId) ?? throw new NotFoundException(L.T("Cobertura não encontrada.", "Cover not found."));
            var hp = Math.Max(0, cover.Hp - Math.Max(0, r.Amount));
            s = ReplaceCover(s, cover with { Hp = hp });
            if (hp == 0) s = s with { Combatants = s.Combatants.Select(c => c.CoverId == coverId ? c with { CoverId = null } : c).ToList() };
            return Task.FromResult(Log(s, null, "cover", L.T($"{cover.Label}: {r.Amount} de dano, {hp} HP{(hp == 0 ? " (destruída)" : "")}.", $"{L.D(cover.Label)}: {r.Amount} damage, {hp} HP{(hp == 0 ? " (destroyed)" : "")}.")));
        }, ct);

    public Task<EncounterDetailDto> TakeCoverAsync(int id, string combatantId, TakeCoverRequest r, CancellationToken ct) =>
        MutateAsync(id, r.Version, (s, _, _) =>
        {
            var c = Require(s, combatantId);
            if (r.CoverId is { } coverId && s.Covers.FirstOrDefault(x => x.Id == coverId) is not { Hp: > 0 })
                throw RequestValidationException.For("coverId", L.T("Essa cobertura não existe ou já foi destruída.", "That cover doesn't exist or was already destroyed."));
            var label = r.CoverId is null ? null : s.Covers.First(x => x.Id == r.CoverId).Label;
            return Task.FromResult(Log(Replace(s, c with { CoverId = r.CoverId }), c.Id, "cover",
                label is null ? L.T($"{c.Name} saiu da cobertura.", $"{c.Name} left cover.") : L.T($"{c.Name} está atrás de {label}.", $"{c.Name} is behind {L.D(label)}.")));
        }, ct);

    // ------------------------------------------------------------------ plumbing

    private async Task<EncounterDetailDto> MutateAsync(int id, int version,
        Func<EncounterState, Encounter, List<string>, Task<EncounterState>> operation, CancellationToken ct)
    {
        var entity = await FindAsync(id, ct);
        if (entity.Version != version)
            throw new ConflictException(L.T("Este combate foi alterado em outra janela. Recarregue para ver a versão atual.", "This fight was changed in another window. Reload to see the current version."));
        var notices = new List<string>();
        _lastAttack = null;
        var state = await operation(Deserialize(entity.StateJson), entity, notices);
        Apply(entity, state, DateTime.UtcNow);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(L.T("Este combate foi alterado ao mesmo tempo em outra janela. Recarregue e tente de novo.", "This fight was changed at the same time in another window. Reload and try again."));
        }
        return await DetailAsync(entity, state, _lastAttack, notices, ct);
    }

    private async Task<EncounterDetailDto> DetailAsync(Encounter e, EncounterState s, AttackResult? attack, IReadOnlyList<string> notices, CancellationToken ct)
    {
        var fighters = await ViewsAsync(s, ct);
        return new EncounterDetailDto(e.Id, e.Name, e.Version, e.Status, s.Round, s.ActiveId, e.CreatedAt, e.UpdatedAt,
            s.Queue.Select(c => fighters[c.Id]).ToList(), s.Covers, s.Log.TakeLast(200).ToList(), s.Notes, attack, notices);
    }

    /// <summary>Every combatant as the table sees it; a PC whose sheet was deleted shows up as a placeholder.</summary>
    private async Task<Dictionary<string, FighterView>> ViewsAsync(EncounterState s, CancellationToken ct)
    {
        var result = new Dictionary<string, FighterView>();
        foreach (var c in s.Combatants)
        {
            if (c.Kind != CombatantKind.Pc) { result[c.Id] = views.ForNpc(c); continue; }
            try
            {
                result[c.Id] = views.ForPc(c, await characters.GetAsync(c.CharacterId!.Value, ct));
            }
            catch (NotFoundException)
            {
                result[c.Id] = new FighterView(c.Id, c.Kind, L.T($"{c.Name} (ficha apagada)", $"{c.Name} (sheet deleted)"), c.Side, c.Initiative, 0, 0, WoundState.Unhurt, 0, null,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], c.Conditions, 0, 0, false, true, false, c.CoverId, c.CharacterId, null, null);
            }
        }
        return result;
    }

    private Combatant Npc(EncounterState s, List<Combatant> added, string name, CombatSide side, CombatantKind kind, CombatSnapshot snapshot) => new()
    {
        Id = NextId(s, added), Kind = kind, Name = Truncate(name, 60), Side = side, Snapshot = snapshot,
        Hp = snapshot.MaxHp, SpBody = snapshot.MaxSpBody, SpHead = snapshot.MaxSpHead,
        Ammo = snapshot.Weapons.Where(w => w.Magazine is not null).ToDictionary(w => w.Key, w => w.Magazine!.Value),
    };

    /// <summary>
    /// Tied Initiatives roll again until the higher number wins (CP:R p.168). A tie already settled (every tie-break set
    /// and distinct, by an earlier roll or a manual move) is kept, so changing someone else does not reshuffle it.
    /// </summary>
    private List<Combatant> BreakTies(List<Combatant> list) =>
        list.Select(c => c.Initiative is null || list.Count(o => o.Initiative == c.Initiative) == 1 ? c with { TieBreak = 0 } : c)
            .GroupBy(c => c.Initiative).SelectMany(g =>
            {
                var group = g.ToList();
                if (g.Key is null || group.Count == 1) return group;
                if (group.All(c => c.TieBreak > 0) && group.Select(c => c.TieBreak).Distinct().Count() == group.Count) return group;
                var rolls = new HashSet<int>();
                return group.Select(c =>
                {
                    int roll;
                    do roll = dice.Roll(10) * 100 + dice.Roll(100); while (!rolls.Add(roll));
                    return c with { TieBreak = roll };
                }).ToList();
            })
            .OrderBy(c => list.FindIndex(o => o.Id == c.Id)).ToList();

    private static string NextId(EncounterState s, List<Combatant> added)
    {
        var used = s.Combatants.Concat(added).Select(c => c.Id).ToHashSet();
        return $"C{Enumerable.Range(1, 9999).First(n => !used.Contains($"C{n}"))}";
    }

    private static string NameOf(string name, int index, int count) => count > 1 ? $"{name} #{index + 1}" : name;

    private static Combatant Require(EncounterState s, string id) => s.Find(id) ?? throw new NotFoundException(L.T("Combatente não encontrado.", "Combatant not found."));

    private static EncounterState Replace(EncounterState s, Combatant c) =>
        s with { Combatants = s.Combatants.Select(x => x.Id == c.Id ? c : x).ToList() };

    private static EncounterState ReplaceCover(EncounterState s, CoverPiece c) =>
        s with { Covers = s.Covers.Select(x => x.Id == c.Id ? c : x).ToList() };

    private static EncounterState Log(EncounterState s, string? actor, string kind, string text) =>
        s with { Log = [.. s.Log, new CombatLogEntry(s.Round, actor, kind, text, DateTime.UtcNow)] };

    public static string ModeLabel(AttackMode mode) => mode switch
    {
        AttackMode.Autofire => "Autofire",
        AttackMode.AimedHead => L.T("mirado na cabeça", "aimed at the head"),
        AttackMode.AimedHand => L.T("mirado na mão", "aimed at the hand"),
        AttackMode.AimedLeg => L.T("mirado na perna", "aimed at the leg"),
        AttackMode.ShotgunShell => "Shotgun Shell",
        AttackMode.Explosive => "explosivo",
        AttackMode.Suppressive => "Suppressive Fire",
        _ => "tiro/golpe",
    };

    private static string Translate(WoundState w) => w switch
    {
        WoundState.Lightly => "Lightly Wounded",
        WoundState.Seriously => L.T("Seriously Wounded (−2 em tudo)", "Seriously Wounded (−2 to everything)"),
        WoundState.Mortally => L.T("Mortally Wounded (−4 em tudo, Death Save todo turno)", "Mortally Wounded (−4 to everything, Death Save every turn)"),
        _ => L.T("sem ferimentos", "unhurt"),
    };

    private static void Apply(Encounter entity, EncounterState state, DateTime now)
    {
        entity.Round = state.Round;
        entity.StateJson = JsonSerializer.Serialize(state, StateJson);
        entity.UpdatedAt = now;
        entity.Version++;
    }

    private static EncounterState Deserialize(string json) =>
        JsonSerializer.Deserialize<EncounterState>(json, StateJson) ?? new EncounterState();

    private async Task<Encounter> FindAsync(int id, CancellationToken ct) =>
        await db.Encounters.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException(L.T($"Combate {id} não encontrado.", $"Fight {id} not found."));

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
