using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

public sealed record SessionFightDto(int Id, string Name, EncounterStatus Status);

public sealed record TableSessionDto(int Id, string Name, DateTime StartedAt, DateTime? EndedAt, bool Open, int Messages, IReadOnlyList<SessionFightDto> Fights);

// empty name = dated
public sealed record NewSessionRequest(string? Name);

public sealed record RenameSessionRequest(string Name);

// There's always one open session: chat lines go there and fights used on the table get stamped with it.
// Lines from before sessions existed get adopted by the first one.
public sealed class TableSessionService(IAppDbContext db, ITableNotifier notifier)
{
    // stops two first requests from both opening a "Session 1"
    private static readonly SemaphoreSlim Opening = new(1, 1);

    // opens "Session 1" (adopting older lines) if there's none
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

    // Closes the open session and starts the next one, named by the GM or "Session N".
    public async Task<TableSessionDto> StartNewAsync(NewSessionRequest r, CancellationToken ct)
    {
        var currentId = await CurrentIdAsync(ct);
        var current = await db.TableSessions.FirstAsync(s => s.Id == currentId, ct);
        var now = DateTime.UtcNow;
        current.EndedAt = now;
        // close any stray open session too (old race)
        foreach (var stray in await db.TableSessions.Where(s => s.EndedAt == null && s.Id != currentId).ToListAsync(ct)) stray.EndedAt = now;
        if (current.Name.Length == 0) current.Name = L.T($"Sessão de {current.StartedAt:dd/MM/yyyy}", $"Session of {current.StartedAt:yyyy-MM-dd}");
        var next = new TableSession { Name = Clean(r.Name) ?? await NextNumberedNameAsync(ct), StartedAt = now };
        db.TableSessions.Add(next);
        await db.SaveChangesAsync(ct);
        await notifier.ChatResetAsync(ct);
        return await DtoAsync(next, ct);
    }

    // past every number in use so a deleted session's number isn't reused
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

    // fights stay on the combat page; the open session can't be deleted
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var session = await FindAsync(id, ct);
        if (session.EndedAt is null) throw new ConflictException(L.T("A sessão aberta não pode ser apagada; comece uma nova antes.", "The open session can't be deleted; start a new one first."));
        await db.TableMessages.Where(m => m.SessionId == id).ExecuteDeleteAsync(ct);
        await db.Encounters.Where(e => e.SessionId == id).ExecuteUpdateAsync(u => u.SetProperty(e => e.SessionId, (int?)null), ct);
        db.TableSessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }

    // the first time it's used on the table
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
