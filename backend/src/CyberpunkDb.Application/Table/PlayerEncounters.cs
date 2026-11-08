using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

// Everyone sees order, Wound State, conditions and crits. PCs show HP/SP to the party. Weapons and Death Save
// only for your own character.
public sealed record PlayerFighterDto(
    string Id, string Name, CombatSide Side, CombatantKind Kind, int? Initiative, WoundState WoundState, bool Dead,
    bool Stabilized, IReadOnlyList<string> Conditions, IReadOnlyList<string> Injuries, int? CharacterId, bool Own,
    int? Hp, int? MaxHp, int? SpBody, int? SpHead, int? MaxSpBody, int? MaxSpHead, bool? MustDeathSave, int? DeathSaveTarget,
    IReadOnlyList<WeaponView>? Weapons, IReadOnlyList<string>? RoundFlags = null);

// the combat log stays with the GM
public sealed record PlayerEncounterDto(int Id, string Name, int Round, string? ActiveId, IReadOnlyList<PlayerFighterDto> Queue,
    PendingView? Pending = null, bool PendingMine = false);

public static class PlayerEncounters
{
    // Combatants hidden from the seat, and enemies without a token yet, stay out of the queue so an ambush isn't
    // given away by the Initiative order.
    public static PlayerEncounterDto For(EncounterDetailDto detail, TableSeat seat, SceneState? scene)
    {
        var hidden = scene?.Tokens.Where(t => t.CombatantId is not null && !SceneViews.CanSee(t, seat)).Select(t => t.CombatantId!).ToHashSet()
                     ?? [];
        if (scene is not null)
        {
            var placed = scene.Tokens.Where(t => t.CombatantId is not null).Select(t => t.CombatantId!).ToHashSet();
            hidden.UnionWith(detail.Queue.Where(f => f.Side == CombatSide.Enemy && !placed.Contains(f.Id)).Select(f => f.Id));
        }
        var queue = detail.Queue.Where(f => !hidden.Contains(f.Id)).Select(f => Fighter(f, seat)).ToList();
        var active = detail.ActiveId is { } id && hidden.Contains(id) ? null : detail.ActiveId;
        // Everyone sees it; the seat that has to answer gets the buttons (target for dodge, attacker for the crit).
        var pending = detail.Pending;
        var mine = pending is not null && seat.CharacterId is { } own
                   && (pending.Step == PendingStep.Defense ? pending.TargetCharacterId == own : pending.AttackerCharacterId == own);
        return new PlayerEncounterDto(detail.Id, detail.Name, detail.Round, active, queue, pending, mine);
    }

    private static PlayerFighterDto Fighter(FighterView f, TableSeat seat)
    {
        var own = f.CharacterId is not null && f.CharacterId == seat.CharacterId;
        // party sees each other's numbers, NPCs only show the Wound State
        var numbers = own || f.Kind == CombatantKind.Pc;
        return new PlayerFighterDto(
            f.Id, f.Name, f.Side, f.Kind, f.Initiative, f.WoundState, f.Dead, f.Stabilized, f.Conditions,
            f.Injuries.Select(i => i.Name).ToList(), f.CharacterId, own,
            numbers ? f.Hp : null, numbers ? f.MaxHp : null, numbers ? f.SpBody : null, numbers ? f.SpHead : null,
            numbers ? f.MaxSpBody : null, numbers ? f.MaxSpHead : null, own ? f.MustDeathSave : null, own ? f.DeathSaveTarget : null,
            own ? f.Weapons : null, own ? f.RoundFlags : null);
    }
}
