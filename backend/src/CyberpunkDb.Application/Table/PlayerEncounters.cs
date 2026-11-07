using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// A fighter as a player sees it. Everyone gets the Initiative order, the Wound State, conditions and Critical
/// Injuries (what the table sees); PCs also show HP and SP to the whole party; weapons and Death Save stay with the
/// player's own character.
/// </summary>
public sealed record PlayerFighterDto(
    string Id, string Name, CombatSide Side, CombatantKind Kind, int? Initiative, WoundState WoundState, bool Dead,
    bool Stabilized, IReadOnlyList<string> Conditions, IReadOnlyList<string> Injuries, int? CharacterId, bool Own,
    int? Hp, int? MaxHp, int? SpBody, int? SpHead, int? MaxSpBody, int? MaxSpHead, bool? MustDeathSave, int? DeathSaveTarget,
    IReadOnlyList<WeaponView>? Weapons, IReadOnlyList<string>? RoundFlags = null);

/// <summary>The fight linked to the active scene, as one seat sees it. The combat log stays with the GM.</summary>
public sealed record PlayerEncounterDto(int Id, string Name, int Round, string? ActiveId, IReadOnlyList<PlayerFighterDto> Queue,
    PendingView? Pending = null, bool PendingMine = false);

public static class PlayerEncounters
{
    /// <summary>
    /// The seat's view. Combatants whose token on the scene is hidden from the seat stay out of the queue, and so do
    /// enemies with no token there yet, so an ambush is not given away by the Initiative order.
    /// </summary>
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
        // A pending attack: everyone sees it; the seat that must answer (the target's player for a dodge, the attacker's
        // for a Critical Injury) gets the buttons.
        var pending = detail.Pending;
        var mine = pending is not null && seat.CharacterId is { } own
                   && (pending.Step == PendingStep.Defense ? pending.TargetCharacterId == own : pending.AttackerCharacterId == own);
        return new PlayerEncounterDto(detail.Id, detail.Name, detail.Round, active, queue, pending, mine);
    }

    private static PlayerFighterDto Fighter(FighterView f, TableSeat seat)
    {
        var own = f.CharacterId is not null && f.CharacterId == seat.CharacterId;
        // The party sees each other's HP and armor; NPCs keep only their Wound State. Weapons and Death Save stay the owner's.
        var numbers = own || f.Kind == CombatantKind.Pc;
        return new PlayerFighterDto(
            f.Id, f.Name, f.Side, f.Kind, f.Initiative, f.WoundState, f.Dead, f.Stabilized, f.Conditions,
            f.Injuries.Select(i => i.Name).ToList(), f.CharacterId, own,
            numbers ? f.Hp : null, numbers ? f.MaxHp : null, numbers ? f.SpBody : null, numbers ? f.SpHead : null,
            numbers ? f.MaxSpBody : null, numbers ? f.MaxSpHead : null, own ? f.MustDeathSave : null, own ? f.DeathSaveTarget : null,
            own ? f.Weapons : null, own ? f.RoundFlags : null);
    }
}
