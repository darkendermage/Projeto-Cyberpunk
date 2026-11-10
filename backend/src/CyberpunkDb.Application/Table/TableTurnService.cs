using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// the seat's view of the fight if there is one, else the character's numbers
public sealed record PlayerReloadResult(PlayerEncounterDto? Encounter, PlayerCharacterDto? Character);

public sealed record PlayerDefenseRequest(bool Dodge);

// inventory copy for PCs. Magazine: an extra one (reload); Mode: the mode to switch to.
public sealed record PlayerWeaponRequest(string Weapon, string? Magazine = null, string? Mode = null);

// Everything a player does in the live fight besides attacking: end turn, reload, unjam, pick up.
public sealed class TableTurnService(IAppDbContext db, EncounterService encounters, Characters.CharacterService characters)
{
    // ownTurn: must be its Turn
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

    // In the live fight it's an Action on its own Turn (CP:R p.171); outside a fight, any time.
    public async Task<PlayerReloadResult> ReloadAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        if (seat.CharacterId is not { } characterId)
            throw RequestValidationException.For("weapon", L.T("Este convite não tem personagem.", "This invite has no character."));
        if (await InFightAsync(characterId, ct))
        {
            var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
            var after = await encounters.ReloadAsync(detail.Id, own.Id, new ReloadCombatantRequest(detail.Version, r.Weapon, r.Magazine), ct);
            return new PlayerReloadResult(PlayerEncounters.For(after, seat, state), null);
        }
        var sheet = await characters.GetAsync(characterId, ct);
        var reloaded = await characters.ReloadAsync(characterId, new Characters.ReloadRequest(sheet.Version, r.Weapon, Magazine: r.Magazine), ct);
        return new PlayerReloadResult(null, PlayerCharacters.From(reloaded));
    }

    // Like a reload: an Action on its own Turn in a fight, any time outside one.
    public async Task<PlayerReloadResult> SwitchModeAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        if (seat.CharacterId is not { } characterId)
            throw RequestValidationException.For("weapon", L.T("Este convite não tem personagem.", "This invite has no character."));
        var mode = r.Mode ?? throw RequestValidationException.For("mode", L.T("Escolha o modo.", "Pick the mode."));
        if (await InFightAsync(characterId, ct))
        {
            var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
            var after = await encounters.SwitchModeAsync(detail.Id, own.Id, new WeaponModeSwitchRequest(detail.Version, r.Weapon, mode), ct);
            return new PlayerReloadResult(PlayerEncounters.For(after, seat, state), null);
        }
        var sheet = await characters.GetAsync(characterId, ct);
        var switched = await characters.SetWeaponModeAsync(characterId, new Characters.WeaponModeRequest(sheet.Version, r.Weapon, mode), ct);
        return new PlayerReloadResult(null, PlayerCharacters.From(switched));
    }

    // Action on its own Turn (CP:R p.344)
    public async Task<PlayerEncounterDto> UnjamAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.UnjamAsync(detail.Id, own.Id, new UnjamRequest(detail.Version, r.Weapon), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    // CP:R p.172
    public async Task<PlayerEncounterDto> DefenseAsync(TableSeat seat, PlayerDefenseRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: false, ct);
        if (detail.Pending is not { Step: PendingStep.Defense } p || p.TargetId != own.Id)
            throw new ConflictException(L.T("Não há tiro esperando a sua esquiva.", "No shot is waiting for your dodge."));
        var after = await encounters.AnswerDefenseAsync(detail.Id, new PendingDefenseRequest(detail.Version, own.Id, r.Dodge), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    // CP:R p.187
    public async Task<PlayerEncounterDto> CriticalAsync(TableSeat seat, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: false, ct);
        if (detail.Pending is not { Step: PendingStep.CriticalInjury } p || p.AttackerId != own.Id)
            throw new ConflictException(L.T("Não há Critical Injury esperando a sua rolagem.", "No Critical Injury is waiting for your roll."));
        var after = await encounters.RollPendingCriticalAsync(detail.Id, new PendingCriticalRequest(detail.Version, p.TargetId), ct);
        return PlayerEncounters.For(after, seat, state);
    }

    // on its own Turn
    public async Task<PlayerEncounterDto> PickUpAsync(TableSeat seat, PlayerWeaponRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.PickUpAsync(detail.Id, own.Id, new UnjamRequest(detail.Version, r.Weapon), ct);
        return PlayerEncounters.For(after, seat, state);
    }

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

    // CP:R p.168. The GM's "Next turn" still forces it.
    public async Task<PlayerEncounterDto> EndTurnAsync(TableSeat seat, CancellationToken ct)
    {
        var (detail, _, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.NextTurnAsync(detail.Id, new EncounterVersionRequest(detail.Version), ct);
        return PlayerEncounters.For(after, seat, state);
    }
}
