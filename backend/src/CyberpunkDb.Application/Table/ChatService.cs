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
    TableVisibility Visibility, int? TargetSeatId, string Text, RollResult? Roll, bool Concealed);

/// <summary>
/// The table chat and its dice. The server rolls; who reads each line is decided here, per seat, both for the history
/// and for the live push: Public reaches everyone, Private the author and the GM, Secret only the GM (its author sees a
/// concealed line), Whisper the author, its target and the GM.
/// </summary>
public sealed class ChatService(IAppDbContext db, ITableNotifier notifier, IDiceRoller dice, CharacterService characters)
{
    public const int HistorySize = 300;
    public const string GmName = "GM";

    public async Task<IReadOnlyList<TableMessageDto>> HistoryAsync(TableSeat? viewer, CancellationToken ct)
    {
        var rows = await db.TableMessages.AsNoTracking().OrderByDescending(m => m.Id).Take(HistorySize).ToListAsync(ct);
        var colors = await ColorsAsync(ct);
        return rows.AsEnumerable().Reverse().Select(m => View(m, viewer, colors)).OfType<TableMessageDto>().ToList();
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

    /// <summary>Starts a new session's history.</summary>
    public async Task ClearAsync(CancellationToken ct) => await db.TableMessages.ExecuteDeleteAsync(ct);

    private async Task<TableMessageDto> PostAsync(TableSeat? author, TableMessageKind kind, string text, RollResult? roll,
        TableVisibility visibility, int? target, CancellationToken ct)
    {
        if (visibility == TableVisibility.Whisper)
        {
            if (target is not { } targetId || targetId == author?.Id || !await db.TableSeats.AnyAsync(s => s.Id == targetId && s.RevokedAt == null, ct))
                throw RequestValidationException.For("targetSeatId", L.T("Escolha para quem sussurrar.", "Choose who to whisper to."));
        }
        else target = null;

        var message = new TableMessage
        {
            At = DateTime.UtcNow, AuthorSeatId = author?.Id, AuthorName = author?.Label ?? GmName, Kind = kind,
            Visibility = visibility, TargetSeatId = target, Text = text,
            RollJson = roll is null ? null : JsonSerializer.Serialize(roll, SceneService.Json),
        };
        db.TableMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var colors = await ColorsAsync(ct);
        var seats = await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);
        var views = seats.Select(s => (s.Id, View: View(message, s, colors))).Where(x => x.View is not null)
            .ToDictionary(x => x.Id, x => x.View!);
        var gm = View(message, null, colors)!;
        await notifier.MessageAsync(gm, views, ct);
        return author is null ? gm : views.GetValueOrDefault(author.Id) ?? gm;
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
        var roll = concealed || m.RollJson is null ? null : JsonSerializer.Deserialize<RollResult>(m.RollJson, SceneService.Json);
        return new TableMessageDto(m.Id, m.At, m.AuthorSeatId, m.AuthorName,
            m.AuthorSeatId is { } seat ? colors.GetValueOrDefault(seat) : null, m.Kind, m.Visibility, m.TargetSeatId, m.Text, roll, concealed);
    }

    private async Task<IReadOnlyDictionary<int, string>> ColorsAsync(CancellationToken ct) =>
        await db.TableSeats.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Color, ct);
}
