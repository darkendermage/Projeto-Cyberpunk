using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// Money and IP are per character (each gets the same amount).
public sealed record TableRewardRequest(IReadOnlyList<int> CharacterIds, int Money, int Ip, string Reason);

public sealed record TableRewardResult(IReadOnlyList<string> Names, int Money, int Ip);

// End of session: the GM pays the group in one go. Each sheet logs it as its own manual entry and the chat says
// who got what.
public sealed class TableRewardService(IAppDbContext db, CharacterService characters, ChatService chat)
{
    public async Task<TableRewardResult> RewardAsync(TableRewardRequest r, CancellationToken ct)
    {
        var ids = (r.CharacterIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw RequestValidationException.For("characterIds", L.T("Escolha pelo menos um personagem.", "Pick at least one character."));
        if (r.Money < 0 || r.Ip < 0) throw RequestValidationException.For("money", L.T("A recompensa só soma: use a ficha para tirar.", "The reward only adds: use the sheet to take away."));
        if (r.Money == 0 && r.Ip == 0) throw RequestValidationException.For("money", L.T("Informe eb, IP ou os dois.", "Enter eb, IP or both."));
        if (string.IsNullOrWhiteSpace(r.Reason)) throw RequestValidationException.For("reason", L.T("Diga o motivo (sessão, missão…).", "Say why (session, mission…)."));

        var rows = await db.Characters.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.Handle ?? c.Name, c.Status }).ToListAsync(ct);
        if (rows.Count != ids.Count) throw new NotFoundException(L.T("Personagem não encontrado.", "Character not found."));
        // checked up front so nobody is paid when one of them can't be
        var creating = rows.Where(c => c.Status == CharacterStatus.Creating).Select(c => c.Name).ToList();
        if (creating.Count > 0)
            throw RequestValidationException.For("characterIds", L.T($"Ainda em criação: {string.Join(", ", creating)}.", $"Still in creation: {string.Join(", ", creating)}."));

        var reason = r.Reason.Trim();
        foreach (var id in ids) await characters.RewardAsync(id, r.Money, r.Ip, reason, ct);

        var names = ids.Select(id => rows.First(c => c.Id == id).Name).ToList();
        var what = string.Join(L.T(" e ", " and "), new[] { r.Money > 0 ? $"{r.Money} eb" : null, r.Ip > 0 ? $"{r.Ip} IP" : null }.OfType<string>());
        var text = L.T($"💰 Recompensa ({reason}): {what} para {string.Join(", ", names)}.", $"💰 Reward ({reason}): {what} to {string.Join(", ", names)}.");
        await chat.SendAsync(null, new SendMessageRequest(text.Length <= 1000 ? text : text[..1000]), ct);
        return new TableRewardResult(names, r.Money, r.Ip);
    }
}
