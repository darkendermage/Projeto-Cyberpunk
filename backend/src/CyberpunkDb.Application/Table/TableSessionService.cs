using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>A fight that took place in a session, with a link to the combat page.</summary>
public sealed record SessionFightDto(int Id, string Name, EncounterStatus Status);

/// <summary>A session of the table as the GM lists it.</summary>
public sealed record TableSessionDto(int Id, string Name, DateTime StartedAt, DateTime? EndedAt, bool Open, int Messages, IReadOnlyList<SessionFightDto> Fights);

/// <summary>"New session": the open session closes with this name (dated when empty).</summary>
public sealed record NewSessionRequest(string? Name);

public sealed record RenameSessionRequest(string Name);

/// <summary>
/// The sessions of the virtual table (E2). One is always open: chat lines go to it and fights started or linked on the
/// table are stamped with it. Lines written before sessions existed are adopted by the first session.
/// </summary>
public sealed class TableSessionService(IAppDbContext db, ITableNotifier notifier)
{
    // One app process serves the table: this keeps two first requests from each opening a "Session 1".
    private static readonly SemaphoreSlim Opening = new(1, 1);

    /// <summary>The open session's id, opening "Session 1" (with the older lines) when there is none.</summary>
    public async Task<int> CurrentIdAsync(CancellationToken ct)
    {
        if (await OpenIdAsync(ct) is { } open) return open;
        await Opening.WaitAsync(ct);
        try
        {
            return await OpenIdAsync(ct) ?? await OpenFirstAsync(ct);
        }
        finally
        {
            Opening.Release();
        }
    }

    private Task<int?> OpenIdAsync(CancellationToken ct) =>
        db.TableSessions.Where(s => s.EndedAt == null).OrderByDescending(s => s.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);

    private async Task<int> OpenFirstAsync(CancellationToken ct)
    {
        var first = await db.TableMessages.OrderBy(m => m.At).Select(m => (DateTime?)m.At).FirstOrDefaultAsync(ct);
        var session = new TableSession { Name = L.T("Sessão 1", "Session 1"), StartedAt = first ?? DateTime.UtcNow };
        db.TableSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await db.TableMessages.Where(m => m.SessionId == null).ExecuteUpdateAsync(u => u.SetProperty(m => m.SessionId, session.Id), ct);
        return session.Id;
    }

    /// <summary>
    /// Closes the open session (it keeps its name) and opens the next one under <paramref name="r"/>'s name, or
    /// "Session N" with a number no session has used (round 6: the sessions page names the session that starts).
    /// </summary>
    public async Task<TableSessionDto> StartNewAsync(NewSessionRequest r, CancellationToken ct)
    {
        var currentId = await CurrentIdAsync(ct);
        var current = await db.TableSessions.FirstAsync(s => s.Id == currentId, ct);
        var now = DateTime.UtcNow;
        current.EndedAt = now;
        // Any other session left open (a race before this fix) closes too.
        foreach (var stray in await db.TableSessions.Where(s => s.EndedAt == null && s.Id != currentId).ToListAsync(ct)) stray.EndedAt = now;
        if (current.Name.Length == 0) current.Name = L.T($"Sessão de {current.StartedAt:dd/MM/yyyy}", $"Session of {current.StartedAt:yyyy-MM-dd}");
        var next = new TableSession { Name = Clean(r.Name) ?? await NextNumberedNameAsync(ct), StartedAt = now };
        db.TableSessions.Add(next);
        await db.SaveChangesAsync(ct);
        await notifier.ChatResetAsync(ct);
        return await DtoAsync(next, ct);
    }

    /// <summary>"Session N" past every number in use (and past the count), so a deleted session's number isn't reused.</summary>
    private async Task<string> NextNumberedNameAsync(CancellationToken ct)
    {
        var names = await db.TableSessions.Select(s => s.Name).ToListAsync(ct);
        var used = names.Select(n => System.Text.RegularExpressions.Regex.Match(n, @"^(?:Sessão|Session) (\d+)$"))
            .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        var n = Math.Max(used, names.Count) + 1;
        return L.T($"Sessão {n}", $"Session {n}");
    }

    public async Task<IReadOnlyList<TableSessionDto>> ListAsync(CancellationToken ct)
    {
        await CurrentIdAsync(ct);
        var sessions = await db.TableSessions.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync(ct);
        var result = new List<TableSessionDto>();
        foreach (var s in sessions) result.Add(await DtoAsync(s, ct));
        return result;
    }

    public async Task<TableSessionDto> RenameAsync(int id, RenameSessionRequest r, CancellationToken ct)
    {
        var session = await FindAsync(id, ct);
        session.Name = Clean(r.Name) ?? throw RequestValidationException.For("name", L.T("Dê um nome à sessão.", "Give the session a name."));
        await db.SaveChangesAsync(ct);
        return await DtoAsync(session, ct);
    }

    /// <summary>Deletes an old session and its chat lines (the fights stay on the combat page). The open one can't go.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var session = await FindAsync(id, ct);
        if (session.EndedAt is null) throw new ConflictException(L.T("A sessão aberta não pode ser apagada; comece uma nova antes.", "The open session can't be deleted; start a new one first."));
        await db.TableMessages.Where(m => m.SessionId == id).ExecuteDeleteAsync(ct);
        await db.Encounters.Where(e => e.SessionId == id).ExecuteUpdateAsync(u => u.SetProperty(e => e.SessionId, (int?)null), ct);
        db.TableSessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Marks a fight as part of the open session, the first time it is used on the table.</summary>
    public async Task StampFightAsync(int encounterId, CancellationToken ct)
    {
        var current = await CurrentIdAsync(ct);
        await db.Encounters.Where(e => e.Id == encounterId && e.SessionId == null).ExecuteUpdateAsync(u => u.SetProperty(e => e.SessionId, current), ct);
    }

    private async Task<TableSession> FindAsync(int id, CancellationToken ct) =>
        await db.TableSessions.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(L.T("Sessão não encontrada.", "Session not found."));

    private async Task<TableSessionDto> DtoAsync(TableSession s, CancellationToken ct)
    {
        var messages = await db.TableMessages.CountAsync(m => m.SessionId == s.Id, ct);
        var fights = await db.Encounters.AsNoTracking().Where(e => e.SessionId == s.Id).OrderBy(e => e.Id)
            .Select(e => new SessionFightDto(e.Id, e.Name, e.Status)).ToListAsync(ct);
        return new TableSessionDto(s.Id, s.Name, s.StartedAt, s.EndedAt, s.EndedAt is null, messages, fights);
    }

    private static string? Clean(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim().Length <= 80 ? name.Trim() : name.Trim()[..80];
}
