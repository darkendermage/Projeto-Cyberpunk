using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>A reload from the table: the fight as the seat sees it when there is one, else the character's numbers.</summary>
public sealed record PlayerReloadResult(PlayerEncounterDto? Encounter, PlayerCharacterDto? Character);

/// <summary>A player's answer to a shot at their character: dodge or not.</summary>
public sealed record PlayerDefenseRequest(bool Dodge);

/// <summary>A weapon of the seat's own character, by its key (the inventory copy for PCs).</summary>
public sealed record PlayerWeaponRequest(string Weapon);

/// <summary>
/// What a player does with their own character in the fight of the live scene, other than attacking: pass the Turn,
/// reload, unjam, pick a thrown weapon up. Each answers the seat's view of the fight.
/// </summary>
public sealed class TableTurnService(IAppDbContext db, EncounterService encounters, Characters.CharacterService characters)
{
    /// <summary>The seat's character's fighter in the live scene's fight; <paramref name="ownTurn"/> demands it is its Turn.</summary>
    internal async Task<(EncounterDetailDto Detail, FighterView Own, SceneState State)> OwnFighterAsync(TableSeat seat, bool ownTurn, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct)
                    ?? throw new ConflictException(L.T("Não há cena aberta na mesa.", "There's no scene open on the table."));
        if (scene.EncounterId is not { } encounterId)
            throw new ConflictException(L.T("Não há combate nesta cena.", "There's no fight on this scene."));
        var detail = await encounters.GetAsync(encounterId, ct);
        if (detail.Status != EncounterStatus.Active) throw new ConflictException(L.T("Este combate foi encerrado.", "This fight has ended."));
        var own = seat.CharacterId is { } id ? detail.Queue.FirstOrDefault(f => f.CharacterId == id) : null;
        if (own is null) throw new ConflictException(L.T("Seu personagem não está neste combate.", "Your character isn't in this fight."));
        if (ownTurn && detail.ActiveId != own.Id) throw new ConflictException(L.T("Aguarde seu turno.", "Wait for your Turn."));
        return (detail, own, SceneService.Deserialize(scene.StateJson));
    }

    /// <summary>
    /// Reloads one of the seat's character's weapons. In the live scene's fight it is an Action on the character's own
    /// Turn (CP:R p.171); out of a fight it can be done any time.
    /// </summary>
    public async Task<PlayerReloadResult> ReloadAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        if (seat.CharacterId is not { } characterId)
            throw RequestValidationException.For("weapon", L.T("Este convite não tem personagem.", "This invite has no character."));
        if (await InFightAsync(characterId, ct))
        {
            var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
            var after = await encounters.ReloadAsync(detail.Id, own.Id, new ReloadCombatantRequest(detail.Version, r.Weapon), ct);
            return new PlayerReloadResult(PlayerEncounters.For(after, seat, state), null);
        }
        var sheet = await characters.GetAsync(characterId, ct);
        var reloaded = await characters.ReloadAsync(characterId, new Characters.ReloadRequest(sheet.Version, r.Weapon), ct);
        return new PlayerReloadResult(null, PlayerCharacters.From(reloaded));
    }

    /// <summary>Clears a jammed weapon of the seat's character: an Action on its own Turn (CP:R p.344).</summary>
    public async Task<PlayerEncounterDto> UnjamAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.UnjamAsync(detail.Id, own.Id, new UnjamRequest(detail.Version, r.Weapon), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    /// <summary>The target's player answers a pending shot at their character: dodge (Evasion roll) or not (CP:R p.172).</summary>
    public async Task<PlayerEncounterDto> DefenseAsync(TableSeat seat, PlayerDefenseRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: false, ct);
        if (detail.Pending is not { Step: PendingStep.Defense } p || p.TargetId != own.Id)
            throw new ConflictException(L.T("Não há tiro esperando a sua esquiva.", "No shot is waiting for your dodge."));
        var after = await encounters.AnswerDefenseAsync(detail.Id, new PendingDefenseRequest(detail.Version, own.Id, r.Dodge), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    /// <summary>The attacker's player rolls the Critical Injury of their pending attack (CP:R p.187).</summary>
    public async Task<PlayerEncounterDto> CriticalAsync(TableSeat seat, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: false, ct);
        if (detail.Pending is not { Step: PendingStep.CriticalInjury } p || p.AttackerId != own.Id)
            throw new ConflictException(L.T("Não há Critical Injury esperando a sua rolagem.", "No Critical Injury is waiting for your roll."));
        var after = await encounters.RollPendingCriticalAsync(detail.Id, new PendingCriticalRequest(detail.Version, p.TargetId), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    /// <summary>Picks a thrown weapon of the seat's character up, on its own Turn.</summary>
    public async Task<PlayerEncounterDto> PickUpAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.PickUpAsync(detail.Id, own.Id, new UnjamRequest(detail.Version, r.Weapon), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    /// <summary>Whether the character fights in the active fight of the live scene.</summary>
    private async Task<bool> InFightAsync(int characterId, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct);
        if (scene?.EncounterId is not { } id) return false;
        try
        {
            var detail = await encounters.GetAsync(id, ct);
            return detail.Status == EncounterStatus.Active && detail.Queue.Any(f => f.CharacterId == characterId);
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>The player ends their own Turn (CP:R p.168); the GM's "Next turn" still forces it.</summary>
    public async Task<PlayerEncounterDto> EndTurnAsync(TableSeat seat, CancellationToken ct)
    {
        var (detail, _, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.NextTurnAsync(detail.Id, new EncounterVersionRequest(detail.Version), ct);
        return PlayerEncounters.For(after, seat, state);
    }
}
