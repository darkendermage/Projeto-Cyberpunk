using System.Text.Json;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Table.Dice;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

public sealed record SendMessageRequest(string Text, TableVisibility Visibility = TableVisibility.Public, int? TargetSeatId = null);

/// <summary>A roll; <see cref="Luck"/>: LUCK points a player spends on their own Check before rolling (+1 each, CP:R p.130).</summary>
public sealed record RollRequest(string Formula, RollKind Kind = RollKind.Plain, string? Label = null, TableVisibility Visibility = TableVisibility.Public, int? TargetSeatId = null, int Luck = 0);

/// <summary>
/// A chat line as one reader sees it. <see cref="Concealed"/>: the author of a secret roll sees that they rolled, not
/// what came up.
/// </summary>
public sealed record TableMessageDto(
    long Id, DateTime At, int? AuthorSeatId, string AuthorName, string? AuthorColor, TableMessageKind Kind,
    TableVisibility Visibility, int? TargetSeatId, string Text, RollResult? Roll, bool Concealed, TableAttack? Attack = null);

/// <summary>
/// The table chat and its dice. The server rolls; who reads each line is decided here, per seat, both for the history
/// and for the live push: Public reaches everyone, Private the author and the GM, Secret only the GM (its author sees a
/// concealed line), Whisper the author, its target and the GM.
/// </summary>
public sealed class ChatService(IAppDbContext db, ITableNotifier notifier, IDiceRoller dice, CharacterService characters, TableSessionService sessions)
{
    public const int HistorySize = 300;
    public const string GmName = "GM";

    public async Task<IReadOnlyList<TableMessageDto>> HistoryAsync(TableSeat? viewer, CancellationToken ct)
    {
        // The open session's lines only; older sessions are read from the GM's session list.
        var session = await sessions.CurrentIdAsync(ct);
        var rows = await db.TableMessages.AsNoTracking().Where(m => m.SessionId == session).OrderByDescending(m => m.Id).Take(HistorySize).ToListAsync(ct);
        var colors = await ColorsAsync(ct);
        return rows.AsEnumerable().Reverse().Select(m => View(m, viewer, colors)).OfType<TableMessageDto>().ToList();
    }

    /// <summary>An archived session's lines, as the GM sees them (every roll and attack, whispers included).</summary>
    public async Task<IReadOnlyList<TableMessageDto>> SessionHistoryAsync(int sessionId, CancellationToken ct)
    {
        var rows = await db.TableMessages.AsNoTracking().Where(m => m.SessionId == sessionId).OrderBy(m => m.Id).ToListAsync(ct);
        var colors = await ColorsAsync(ct);
        return rows.Select(m => View(m, null, colors)).OfType<TableMessageDto>().ToList();
    }

    public async Task<TableMessageDto> SendAsync(TableSeat? author, SendMessageRequest r, CancellationToken ct)
    {
        var text = r.Text?.Trim() ?? "";
        if (text.Length is 0 or > 1000)
            throw RequestValidationException.For("text", L.T("Escreva uma mensagem (até 1000 caracteres).", "Write a message (up to 1000 characters)."));
        return await PostAsync(author, TableMessageKind.Text, text, null, r.Visibility, r.TargetSeatId, ct);
    }

    public async Task<TableMessageDto> RollAsync(TableSeat? author, RollRequest r, CancellationToken ct)
    {
        var roll = TableRoller.Roll(r.Formula, r.Kind, dice);
        if (r.Luck != 0)
        {
            if (r.Luck < 0 || author?.CharacterId is not { } characterId || r.Kind != RollKind.Check)
                throw RequestValidationException.For("luck", L.T("Sorte só vale num teste (1d10) do seu próprio personagem.", "LUCK only goes on a Check (1d10) of your own character."));
            await characters.SpendLuckAsync(characterId, r.Luck, ct);
            roll = roll with { Total = roll.Total + r.Luck, Luck = r.Luck };
        }
        var label = r.Label?.Trim();
        var text = string.IsNullOrEmpty(label) ? roll.Formula : label.Length <= 80 ? label : label[..80];
        return await PostAsync(author, TableMessageKind.Roll, text, roll, r.Visibility, r.TargetSeatId, ct);
    }

    /// <summary>
    /// The table's reroll (a house rule): 1 LUCK rolls a player's own Check again, keeping its modifiers and the LUCK
    /// already spent; the new line points at the one it replaces, and a line is rerolled only once.
    /// </summary>
    public async Task<TableMessageDto> RerollAsync(TableSeat author, long messageId, CancellationToken ct)
    {
        var m = await db.TableMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == messageId, ct);
        var old = m?.RollJson is { } json ? JsonSerializer.Deserialize<RollResult>(json, SceneService.Json) : null;
        if (m is null || old is null || m.AuthorSeatId != author.Id || old.Kind != RollKind.Check || author.CharacterId is not { } characterId)
            throw RequestValidationException.For("messageId", L.T("Só dá para rerrolar um teste seu.", "You can only reroll a Check of yours."));
        var later = await db.TableMessages.AsNoTracking().Where(x => x.Id > messageId && x.RollJson != null).Select(x => x.RollJson!).ToListAsync(ct);
        if (later.Any(j => JsonSerializer.Deserialize<RollResult>(j, SceneService.Json)?.RerollOf == messageId))
            throw RequestValidationException.For("messageId", L.T("Essa rolagem já foi rerrolada.", "That roll was already rerolled."));
        await characters.SpendLuckAsync(characterId, 1, ct);
        var again = TableRoller.Roll(old.Formula, RollKind.Check, dice);
        again = again with { Total = again.Total + old.Luck, Luck = old.Luck, RerollOf = messageId };
        var text = $"{m.Text} {L.T("(rerrolada)", "(rerolled)")}";
        return await PostAsync(author, TableMessageKind.Roll, text.Length <= 100 ? text : text[..100], again, m.Visibility, m.TargetSeatId, ct);
    }

    /// <summary>
    /// The line of an attack of the fight on the live scene, written by the attacker's seat when a player's character
    /// made it (the GM otherwise). Everyone reads it; players see NPC numbers only as a hit or a miss.
    /// </summary>
    public async Task PostAttackAsync(TableAttack attack, int? characterId, CancellationToken ct)
    {
        var seat = characterId is { } id ? await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.CharacterId == id && s.RevokedAt == null, ct) : null;
        var text = L.T($"{attack.AttackerName} ataca com {attack.Weapon}", $"{attack.AttackerName} attacks with {attack.Weapon}");
        await PostCoreAsync(seat?.Id, seat?.Label ?? GmName, TableMessageKind.Attack, text.Length <= 200 ? text : text[..200],
            JsonSerializer.Serialize(attack, SceneService.Json), TableVisibility.Public, null, ct);
    }

    /// <summary>Marks the line of an attack the GM undid, and sends it again so every screen crosses it out.</summary>
    public async Task MarkAttackUndoneAsync(int encounterId, int seq, CancellationToken ct)
    {
        var recent = await db.TableMessages.Where(m => m.Kind == TableMessageKind.Attack).OrderByDescending(m => m.Id).Take(50).ToListAsync(ct);
        foreach (var m in recent)
        {
            var attack = JsonSerializer.Deserialize<TableAttack>(m.RollJson ?? "null", SceneService.Json);
            if (attack is null || attack.EncounterId != encounterId || attack.Seq != seq) continue;
            m.RollJson = JsonSerializer.Serialize(attack with { Undone = true }, SceneService.Json);
            await db.SaveChangesAsync(ct);
            await PublishAsync(m, ct);
            return;
        }
    }


    private Task<TableMessageDto> PostAsync(TableSeat? author, TableMessageKind kind, string text, RollResult? roll,
        TableVisibility visibility, int? target, CancellationToken ct) =>
        PostCoreAsync(author?.Id, author?.Label ?? GmName, kind, text, roll is null ? null : JsonSerializer.Serialize(roll, SceneService.Json), visibility, target, ct);

    private async Task<TableMessageDto> PostCoreAsync(int? authorId, string authorName, TableMessageKind kind, string text, string? payload,
        TableVisibility visibility, int? target, CancellationToken ct)
    {
        if (visibility == TableVisibility.Whisper)
        {
            if (target is not { } targetId || targetId == authorId || !await db.TableSeats.AnyAsync(s => s.Id == targetId && s.RevokedAt == null, ct))
                throw RequestValidationException.For("targetSeatId", L.T("Escolha para quem sussurrar.", "Choose who to whisper to."));
        }
        else target = null;

        var message = new TableMessage
        {
            At = DateTime.UtcNow, AuthorSeatId = authorId, AuthorName = authorName.Length <= 60 ? authorName : authorName[..60], Kind = kind,
            Visibility = visibility, TargetSeatId = target, Text = text, RollJson = payload, SessionId = await sessions.CurrentIdAsync(ct),
        };
        db.TableMessages.Add(message);
        await db.SaveChangesAsync(ct);
        var (gm, views) = await PublishAsync(message, ct);
        return authorId is { } id ? views.GetValueOrDefault(id) ?? gm : gm;
    }

    /// <summary>Sends a line to every screen allowed to read it, each with its own view.</summary>
    private async Task<(TableMessageDto Gm, Dictionary<int, TableMessageDto> Seats)> PublishAsync(TableMessage message, CancellationToken ct)
    {
        var colors = await ColorsAsync(ct);
        var seats = await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);
        var views = seats.Select(s => (s.Id, View: View(message, s, colors))).Where(x => x.View is not null)
            .ToDictionary(x => x.Id, x => x.View!);
        var gm = View(message, null, colors)!;
        await notifier.MessageAsync(gm, views, ct);
        return (gm, views);
    }

    /// <summary>The line as <paramref name="viewer"/> reads it (null viewer: the GM); null when they may not.</summary>
    private static TableMessageDto? View(TableMessage m, TableSeat? viewer, IReadOnlyDictionary<int, string> colors)
    {
        var mine = viewer is not null && m.AuthorSeatId == viewer.Id;
        var visible = viewer is null || m.Visibility switch
        {
            TableVisibility.Public => true,
            TableVisibility.Private or TableVisibility.Secret => mine,
            TableVisibility.Whisper => mine || m.TargetSeatId == viewer.Id,
            _ => false,
        };
        if (!visible) return null;
        var concealed = viewer is not null && m.Visibility == TableVisibility.Secret;
        TableAttack? attack = null;
        RollResult? roll = null;
        if (m.Kind == TableMessageKind.Attack)
        {
            attack = m.RollJson is null ? null : JsonSerializer.Deserialize<TableAttack>(m.RollJson, SceneService.Json);
            if (attack is not null && viewer is not null) attack = TableAttacks.ForSeat(attack);
        }
        else if (!concealed && m.RollJson is not null) roll = JsonSerializer.Deserialize<RollResult>(m.RollJson, SceneService.Json);
        return new TableMessageDto(m.Id, m.At, m.AuthorSeatId, m.AuthorName,
            m.AuthorSeatId is { } seat ? colors.GetValueOrDefault(seat) : null, m.Kind, m.Visibility, m.TargetSeatId, m.Text, roll, concealed, attack);
    }

    private async Task<IReadOnlyDictionary<int, string>> ColorsAsync(CancellationToken ct) =>
        await db.TableSeats.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Color, ct);
}
