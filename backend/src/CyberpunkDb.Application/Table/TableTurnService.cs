using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// What a player does with their own character in the fight of the live scene, other than attacking: pass the Turn,
/// reload, unjam, pick a thrown weapon up. Each answers the seat's view of the fight.
/// </summary>
public sealed class TableTurnService(IAppDbContext db, EncounterService encounters)
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

    /// <summary>The player ends their own Turn (CP:R p.168); the GM's "Next turn" still forces it.</summary>
    public async Task<PlayerEncounterDto> EndTurnAsync(TableSeat seat, CancellationToken ct)
    {
        var (detail, _, state) = await OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await encounters.NextTurnAsync(detail.Id, new EncounterVersionRequest(detail.Version), ct);
        return PlayerEncounters.For(after, seat, state);
    }
}
