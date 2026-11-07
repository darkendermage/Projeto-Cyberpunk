using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

/// <summary>A PC on the scene, as the strip under the map shows it: name, HP, Wound State and SP of head and body.</summary>
public sealed record PartyMemberDto(
    int CharacterId, string Name, int Hp, int MaxHp, WoundState WoundState, int SpHead, int? MaxSpHead, int SpBody, int? MaxSpBody);

/// <summary>
/// The characters on a scene (the players see each other's numbers; NPCs stay with the fight's Wound State), and a
/// player's own armor adjusted from the table.
/// </summary>
public sealed class TablePartyService(SceneService scenes, CharacterService characters)
{
    /// <summary>The PCs whose token the seat sees on the live scene.</summary>
    public async Task<IReadOnlyList<PartyMemberDto>> ForSeatAsync(TableSeat seat, CancellationToken ct) =>
        await MembersAsync((await scenes.PlayerSceneAsync(seat, ct))?.Tokens ?? [], ct);

    /// <summary>Every PC with a token on the scene (the GM's view).</summary>
    public async Task<IReadOnlyList<PartyMemberDto>> ForSceneAsync(int sceneId, CancellationToken ct) =>
        await MembersAsync((await scenes.GetAsync(sceneId, ct)).Tokens, ct);

    /// <summary>The seat's own armor up or down; the fight and the party follow through the sheet's broadcast.</summary>
    public async Task<PlayerCharacterDto> AdjustOwnArmorAsync(TableSeat seat, ArmorAdjustRequest r, CancellationToken ct)
    {
        if (seat.CharacterId is not { } id)
            throw RequestValidationException.For("location", L.T("Este convite não tem personagem.", "This invite has no character."));
        // The sheet's own broadcast (CharacterBroadcast) refreshes the fight and the party.
        var detail = await characters.AdjustArmorAsync(id, r.Location, r.Delta, L.T("Mesa virtual", "Virtual table"), ct);
        return PlayerCharacters.From(detail);
    }

    private async Task<IReadOnlyList<PartyMemberDto>> MembersAsync(IEnumerable<TokenView> tokens, CancellationToken ct)
    {
        var members = new List<PartyMemberDto>();
        foreach (var id in tokens.Where(t => t.CharacterId is not null).Select(t => t.CharacterId!.Value).Distinct())
        {
            try
            {
                var c = await characters.GetAsync(id, ct);
                var r = c.Report;
                members.Add(new PartyMemberDto(c.Id, c.Sheet.Identity.Handle is { Length: > 0 } h ? h : c.Sheet.Identity.Name, r.Health.CurrentHp, r.Health.MaxHp,
                    r.Health.WoundState, r.Armor.Head.Sp, r.Armor.Head.MaxSp, r.Armor.Body.Sp, r.Armor.Body.MaxSp));
            }
            catch (NotFoundException) { /* a token of a deleted character */ }
        }
        return members;
    }
}

/// <summary>SP of one location up or down from the table's +/−.</summary>
public sealed record ArmorAdjustRequest(ArmorSlot Location, int Delta);
