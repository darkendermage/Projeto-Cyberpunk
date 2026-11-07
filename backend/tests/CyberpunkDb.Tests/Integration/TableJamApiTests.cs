using System.Net;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>A Poor weapon jams on a natural 1 and is unjammed with an Action (CP:R p.344).</summary>
[Collection(ApiCollection.Name)]
public class TableJamApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    /// <summary>The Solo's first gun made Poor and loaded, then fired at the ganger with a natural 1.</summary>
    private async Task<(TableScenario.Fight Fight, string Gun)> JammedGun(string name)
    {
        var s = await _t.FightOnTheTable(name);
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        var gun = sheet.Report.Weapons.First(w => w.Equipped && w.Magazine is not null).InstanceId;
        sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapon-quality", new WeaponQualityRequest(sheet.Version, gun, WeaponQuality.Poor));
        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/reload", new ReloadRequest(sheet.Version, gun));
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack",
            new { version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = gun, mode = "Single", range = 5, attackRoll = 1, attackExtra = 2 });
        return (s, gun);
    }

    private static JsonNode GunIn(JsonNode fight, string combatantId, string gun) =>
        fight["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == combatantId)!["weapons"]!.AsArray().First(w => w!["key"]!.GetValue<string>() == gun)!;

    [Fact]
    public async Task Poor_weapon_jams_on_natural_1_and_cannot_fire()
    {
        var (s, gun) = await JammedGun("Travou");
        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        GunIn(fight, s.PcCombatantId, gun)["jammed"]!.GetValue<bool>().Should().BeTrue();

        var again = await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack",
            new { version = await _t.VersionOf(s.EncounterId), attackerId = s.PcCombatantId, targetIds = new[] { s.GangerIds[0] }, weapon = gun, mode = "Single", range = 5 });
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unjam_clears_and_logs()
    {
        var (s, gun) = await JammedGun("Destrava");

        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/unjam",
            new { version = await _t.VersionOf(s.EncounterId), weapon = gun });

        GunIn(fight, s.PcCombatantId, gun)["jammed"]!.GetValue<bool>().Should().BeFalse();
        fight["log"]!.AsArray().Last()!["text"]!.GetValue<string>().Should().Contain("destrava");
    }

    [Fact]
    public async Task Player_unjam_only_on_own_turn()
    {
        var (s, gun) = await JammedGun("DestravaNaVez");
        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/unjam", new { weapon = gun }, s.Seat.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _t.MakeActive(s.EncounterId, s.PcCombatantId);
        var view = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Post, "/api/table/play/unjam", new { weapon = gun }, s.Seat.Token);
        view.Queue.Single(f => f.Own).Weapons!.Single(w => w.Key == gun).Jammed.Should().BeFalse();
    }
}
