using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Compendium;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// One combatant per chosen token (PC, saved NPC, compendium NPC with a sheet, or a generic named after the
// token). Then the scene and tokens get linked to the fight.
public sealed class TableFightService(IAppDbContext db, SceneService scenes, EncounterService encounters, NpcCompendium compendium, CombatRuleBook rules)
{
    // On a scene with a fight it's also a cover there, so attacks through it wear it down.
    public async Task<SceneView> AddBarrierAsync(int sceneId, BarrierRequest r, CancellationToken ct)
    {
        var table = rules.CoverHp(r.Material, r.Thick); // throws if the material doesn't exist
        var hp = r.Hp ?? table;
        if (hp is <= 0 or > 999)
            throw RequestValidationException.For("material", L.T($"Isso não para bala: não é cobertura ({rules.Document.CoverRules.Source}).", $"That doesn't stop bullets: it isn't cover ({rules.Document.CoverRules.Source})."));
        var scene = await scenes.GetAsync(sceneId, ct);
        string? coverId = null;
        if (scene.EncounterId is { } encounterId)
            coverId = await AddCoverAsync(encounterId, r.Label, r.Material, r.Thick, hp, ct);
        return await scenes.AddBarrierAsync(sceneId, r, hp, coverId, ct);
    }

    // repair only outside a fight; covers take it on the fight
    public async Task<SceneView> DamageBarrierAsync(int sceneId, string tokenId, BarrierDamageRequest r, CancellationToken ct)
    {
        var scene = await scenes.GetAsync(sceneId, ct);
        var token = scene.Tokens.FirstOrDefault(t => t.Id == tokenId) ?? throw new NotFoundException(L.T("Token não encontrado.", "Token not found."));
        var barrier = token.Barrier ?? throw RequestValidationException.For("tokenId", L.T("Esse token não é uma barreira.", "That token isn't a barrier."));
        if (barrier.CoverId is { } coverId && scene.EncounterId is { } encounterId)
        {
            var fight = await encounters.GetAsync(encounterId, ct);
            if (fight.Covers.Any(c => c.Id == coverId))
            {
                if (r.Amount < 0)
                    throw RequestValidationException.For("amount", L.T("Esta barreira é cobertura do combate: o combate não repara cobertura.", "This barrier is the fight's cover: the fight doesn't repair cover."));
                await encounters.DamageCoverAsync(encounterId, coverId, new CoverDamageRequest(fight.Version, r.Amount), ct);
                return await scenes.GetAsync(sceneId, ct);
            }
        }
        return await scenes.SetBarrierHpAsync(sceneId, tokenId, r.Amount, ct);
    }

    private async Task<string> AddCoverAsync(int encounterId, string? label, string material, bool thick, int hp, CancellationToken ct)
    {
        var fight = await encounters.GetAsync(encounterId, ct);
        var before = fight.Covers.Select(c => c.Id).ToHashSet();
        fight = await encounters.AddCoverAsync(encounterId, new CoverRequest(fight.Version, label ?? "", material, thick, hp), ct);
        return fight.Covers.First(c => !before.Contains(c.Id)).Id;
    }

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
                // same character twice: fights once, through the first token
                if (token.CharacterId is { } c && !characters.Add(c)) continue;
                var before = fight.Queue.Select(f => f.Id).ToHashSet();
                fight = await encounters.AddCombatantsAsync(fight.Id, Combatant(fight.Version, side, token, existing), ct);
                links[token.Id] = fight.Queue.First(f => !before.Contains(f.Id)).Id;
            }
            await scenes.LinkFightAsync(sceneId, fight.Id, links, ct);
            var covers = new Dictionary<string, string>();
            foreach (var t in scene.Tokens.Where(t => t.Barrier is { Hp: > 0 }))
                covers[t.Id] = await AddCoverAsync(fight.Id, t.Label, t.Barrier!.Material, t.Barrier.Thick, t.Barrier.Hp, ct);
            return covers.Count == 0 ? await scenes.GetAsync(sceneId, ct) : await scenes.LinkBarriersAsync(sceneId, covers, ct);
        }
        catch
        {
            // don't leave a half-built fight behind
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
