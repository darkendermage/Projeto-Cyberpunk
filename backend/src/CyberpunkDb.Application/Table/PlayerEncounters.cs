using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// A fighter as a player sees it. Everyone gets the Initiative order, the Wound State, conditions and Critical
/// Injuries (what the table sees); only the player's own character shows its numbers and weapons.
/// </summary>
public sealed record PlayerFighterDto(
    string Id, string Name, CombatSide Side, CombatantKind Kind, int? Initiative, WoundState WoundState, bool Dead,
    bool Stabilized, IReadOnlyList<string> Conditions, IReadOnlyList<string> Injuries, int? CharacterId, bool Own,
    int? Hp, int? MaxHp, int? SpBody, int? SpHead, int? MaxSpBody, int? MaxSpHead, bool? MustDeathSave, int? DeathSaveTarget,
    IReadOnlyList<WeaponView>? Weapons);

/// <summary>The fight linked to the active scene, as one seat sees it. The combat log stays with the GM.</summary>
public sealed record PlayerEncounterDto(int Id, string Name, int Round, string? ActiveId, IReadOnlyList<PlayerFighterDto> Queue);

public static class PlayerEncounters
{
    /// <summary>
    /// The seat's view. Combatants whose token on the scene is hidden from the seat stay out of the queue, so an
    /// ambush is not given away by the Initiative order.
    /// </summary>
    public static PlayerEncounterDto For(EncounterDetailDto detail, TableSeat seat, SceneState? scene)
    {
        var hidden = scene?.Tokens.Where(t => t.CombatantId is not null && !SceneViews.CanSee(t, seat)).Select(t => t.CombatantId!).ToHashSet()
                     ?? [];
        var queue = detail.Queue.Where(f => !hidden.Contains(f.Id)).Select(f => Fighter(f, seat)).ToList();
        var active = detail.ActiveId is { } id && hidden.Contains(id) ? null : detail.ActiveId;
        return new PlayerEncounterDto(detail.Id, detail.Name, detail.Round, active, queue);
    }

    private static PlayerFighterDto Fighter(FighterView f, TableSeat seat)
    {
        var own = f.CharacterId is not null && f.CharacterId == seat.CharacterId;
        return new PlayerFighterDto(
            f.Id, f.Name, f.Side, f.Kind, f.Initiative, f.WoundState, f.Dead, f.Stabilized, f.Conditions,
            f.Injuries.Select(i => i.Name).ToList(), f.CharacterId, own,
            own ? f.Hp : null, own ? f.MaxHp : null, own ? f.SpBody : null, own ? f.SpHead : null,
            own ? f.MaxSpBody : null, own ? f.MaxSpHead : null, own ? f.MustDeathSave : null, own ? f.DeathSaveTarget : null,
            own ? f.Weapons : null);
    }
}
