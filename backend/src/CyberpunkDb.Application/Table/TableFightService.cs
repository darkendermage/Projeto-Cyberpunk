using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Compendium;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// A fight started from the table: one combatant per chosen token, standing for what the token stands for (the PC,
/// the saved NPC, the compendium NPC with a sheet; anything else a generic enemy named after the token). The scene is
/// then linked to the fight and each token to its combatant.
/// </summary>
public sealed class TableFightService(IAppDbContext db, SceneService scenes, EncounterService encounters, NpcCompendium compendium)
{
    public const int MaxTokens = 60;

    public async Task<SceneView> StartAsync(int sceneId, CreateFightRequest r, CancellationToken ct)
    {
        var entries = (r.Tokens ?? []).DistinctBy(e => e.TokenId).ToList();
        if (entries.Count == 0)
            throw RequestValidationException.For("tokens", L.T("Escolha ao menos um token para o combate.", "Pick at least one token for the fight."));
        if (entries.Count > MaxTokens)
            throw RequestValidationException.For("tokens", L.T($"Um combate começa com até {MaxTokens} tokens.", $"A fight starts with up to {MaxTokens} tokens."));
        var scene = await scenes.GetAsync(sceneId, ct);
        var chosen = entries.Select(e => (e.Side, Token: scene.Tokens.FirstOrDefault(t => t.Id == e.TokenId)
            ?? throw RequestValidationException.For("tokens", L.T("Esse token não está na cena.", "That token isn't on the scene.")))).ToList();
        var savedNpcs = chosen.Where(c => c.Token.NpcId is not null).Select(c => c.Token.NpcId!.Value).Distinct().ToList();
        var existing = (await db.Npcs.AsNoTracking().Where(n => savedNpcs.Contains(n.Id)).Select(n => n.Id).ToListAsync(ct)).ToHashSet();

        var fight = await encounters.CreateAsync(new CreateEncounterRequest(r.Name), ct);
        try
        {
            var links = new Dictionary<string, string>();
            var characters = new HashSet<int>();
            foreach (var (side, token) in chosen)
            {
                // Two tokens of one character: the character fights once, through the first.
                if (token.CharacterId is { } c && !characters.Add(c)) continue;
                var before = fight.Queue.Select(f => f.Id).ToHashSet();
                fight = await encounters.AddCombatantsAsync(fight.Id, Combatant(fight.Version, side, token, existing), ct);
                links[token.Id] = fight.Queue.First(f => !before.Contains(f.Id)).Id;
            }
            return await scenes.LinkFightAsync(sceneId, fight.Id, links, ct);
        }
        catch
        {
            // All or nothing: a half-built fight is not left behind.
            await encounters.DeleteAsync(fight.Id, CancellationToken.None);
            throw;
        }
    }

    private AddCombatantsRequest Combatant(int version, CombatSide side, TokenView t, IReadOnlySet<int> savedNpcs) =>
        t.CharacterId is { } c ? new AddCombatantsRequest(version, side, CharacterId: c)
        : t.NpcId is { } n && savedNpcs.Contains(n) ? new AddCombatantsRequest(version, side, NpcId: n, Name: t.Label)
        : t.CompendiumNpcId is { } b && compendium.Find(b)?.Sheet is not null ? new AddCombatantsRequest(version, side, CompendiumNpcId: b, Name: t.Label)
        : new AddCombatantsRequest(version, side, Generic: new GenericRequest(t.Label));
}
