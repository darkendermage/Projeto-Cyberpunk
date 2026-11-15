using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

public sealed record PlayerCyberRequest(string Slug);

public sealed record GmCyberRequest(string CombatantId, string Slug);

// Implants switched on at the table (Sandevistan, Berserk, Flashbulb...): the fight applies the effect and the chat
// says what happened and what the targets roll. The player does it on its own Turn; the GM any time.
public sealed class TableCyberService(IAppDbContext db, EncounterService encounters, TableTurnService turns, ChatService chat)
{
    public async Task<PlayerEncounterDto> PlayerAsync(TableSeat seat, PlayerCyberRequest r, CancellationToken ct)
    {
        var (detail, own, state) = await turns.OwnFighterAsync(seat, ownTurn: true, ct);
        var after = await ActivateAsync(detail, own.Id, r.Slug, seat, ct);
        return PlayerEncounters.For(after, seat, state);
    }

    public async Task<EncounterDetailDto> GmAsync(int sceneId, GmCyberRequest r, CancellationToken ct)
    {
        var encounterId = await db.Scenes.AsNoTracking().Where(s => s.Id == sceneId).Select(s => s.EncounterId).FirstOrDefaultAsync(ct)
                          ?? throw RequestValidationException.For("sceneId", L.T("Esta cena não tem combate.", "This scene has no fight."));
        return await ActivateAsync(await encounters.GetAsync(encounterId, ct), r.CombatantId, r.Slug, null, ct);
    }

    private async Task<EncounterDetailDto> ActivateAsync(EncounterDetailDto detail, string combatantId, string slug, TableSeat? seat, CancellationToken ct)
    {
        var fighter = detail.Queue.FirstOrDefault(f => f.Id == combatantId) ?? throw new NotFoundException(L.T("Combatente não encontrado.", "Combatant not found."));
        var trait = fighter.Traits?.FirstOrDefault(t => t.Slug == slug && t.Activate is not null)
                    ?? throw RequestValidationException.For("slug", L.T("Esse cyberware não é ativável neste personagem.", "That cyberware can't be switched on for this character."));
        var after = await encounters.ActivateCyberAsync(detail.Id, combatantId, new CyberActivateRequest(detail.Version, slug), ct);
        var line = after.Log.LastOrDefault()?.Text ?? trait.Note;
        var text = L.T($"⚡ {fighter.Name} ativa {trait.Label}. {line}", $"⚡ {fighter.Name} switches on {trait.Label}. {line}");
        await chat.SendAsync(seat, new SendMessageRequest(text.Length <= 1000 ? text : text[..1000]), ct);
        return after;
    }
}
