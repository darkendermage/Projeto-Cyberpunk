using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// WeaponMode: weapon-rules.json mode (Slug/Shell, Burst, Throw...), null = the weapon's current one
public sealed record QuickAttackRequest(string Weapon, AttackMode Mode, List<string> TargetIds, string? WeaponMode = null);

// any fighter in the scene's fight
public sealed record GmQuickAttackRequest(string AttackerId, string Weapon, AttackMode Mode, List<string> TargetIds, string? WeaponMode = null);

// Resolved like on the combat table; distance comes from the map. Area attacks need a template, so they stay on
// the combat page.
public sealed class TableAttackService(IAppDbContext db, EncounterService encounters)
{
    // own character, own Turn, only visible targets
    public async Task<QuickAttackResult> PlayerAttackAsync(TableSeat seat, QuickAttackRequest r, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active, ct)
                    ?? throw new ConflictException(L.T("Não há cena aberta na mesa.", "There's no scene open on the table."));
        var (detail, state) = await FightAsync(scene, ct);
        var own = seat.CharacterId is { } characterId ? detail.Queue.FirstOrDefault(f => f.CharacterId == characterId) : null;
        if (own is null) throw new ConflictException(L.T("Seu personagem não está neste combate.", "Your character isn't in this fight."));
        if (detail.ActiveId != own.Id) throw new ConflictException(L.T("Aguarde seu turno para atacar.", "Wait for your Turn to attack."));
        var visible = PlayerEncounters.For(detail, seat, state).Queue.Select(f => f.Id).ToHashSet();
        if (r.TargetIds.Any(id => !visible.Contains(id)))
            throw RequestValidationException.For("targetIds", L.T("Escolha um alvo que você vê no mapa.", "Pick a target you can see on the map."));
        var result = await AttackAsync(detail, state, own.Id, r.Weapon, r.Mode, r.TargetIds, r.WeaponMode, ct);
        return result with { Attack = result.Attack is { } a ? TableAttacks.ForSeat(a) : null };
    }

    // any fighter, Turn or not
    public async Task<QuickAttackResult> GmAttackAsync(int sceneId, GmQuickAttackRequest r, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sceneId, ct)
                    ?? throw new NotFoundException(L.T("Cena não encontrada.", "Scene not found."));
        var (detail, state) = await FightAsync(scene, ct);
        return await AttackAsync(detail, state, r.AttackerId, r.Weapon, r.Mode, r.TargetIds, r.WeaponMode, ct);
    }

    private async Task<(EncounterDetailDto, SceneState)> FightAsync(Scene scene, CancellationToken ct)
    {
        if (scene.EncounterId is not { } encounterId)
            throw new ConflictException(L.T("Esta cena não tem combate ligado.", "This scene has no fight linked."));
        var detail = await encounters.GetAsync(encounterId, ct);
        if (detail.Status != EncounterStatus.Active) throw new ConflictException(L.T("Este combate foi encerrado.", "This fight has ended."));
        return (detail, SceneService.Deserialize(scene.StateJson));
    }

    private async Task<QuickAttackResult> AttackAsync(EncounterDetailDto detail, SceneState state, string attackerId, string weapon, AttackMode mode,
        List<string> targetIds, string? weaponMode, CancellationToken ct)
    {
        if (mode is AttackMode.Suppressive or AttackMode.Explosive)
            throw RequestValidationException.For("mode", L.T("Ataques em área ficam no formulário completo da tela de combate.", "Area attacks stay on the combat table's full form."));
        if (targetIds.Count == 0) throw RequestValidationException.For("targetIds", L.T("Escolha o alvo.", "Pick the target."));
        var attacker = detail.Queue.FirstOrDefault(f => f.Id == attackerId) ?? throw new NotFoundException(L.T("Atacante não está no combate.", "The attacker isn't in the fight."));
        var view = AttackResolver.SelectWeapon(attacker, weapon, weaponMode);

        int? range = null;
        if (!view.Melee)
        {
            var from = TokenOf(state, attacker) ?? throw RequestValidationException.For("attackerId", L.T($"Coloque o token de {attacker.Name} no mapa.", $"Put {attacker.Name}'s token on the map."));
            var target = detail.Queue.FirstOrDefault(f => f.Id == targetIds[0]) ?? throw new NotFoundException(L.T("Alvo não está no combate.", "The target isn't in the fight."));
            var to = TokenOf(state, target) ?? throw RequestValidationException.For("targetIds", L.T($"Coloque o token de {target.Name} no mapa.", $"Put {target.Name}'s token on the map."));
            range = (int)Math.Round(TableAttacks.Meters(state.Grid, from, to));
        }

        // waits for dodge (REF 8+) and the crit roll
        var after = await encounters.AttackAsync(detail.Id, new AttackRequest(detail.Version, attackerId, targetIds, weapon, mode, range, Interactive: true, WeaponMode: weaponMode), ct);
        return after.LastAttack is { } done ? new QuickAttackResult(TableAttacks.From(after, done), null) : new QuickAttackResult(null, after.Pending);
    }

    // linked to the combatant, or a PC token linked to the sheet
    private static SceneToken? TokenOf(SceneState state, FighterView f) =>
        state.Tokens.FirstOrDefault(t => t.CombatantId == f.Id)
        ?? (f.CharacterId is { } id ? state.Tokens.FirstOrDefault(t => t.CharacterId == id) : null);
}
