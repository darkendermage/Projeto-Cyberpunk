using System.Net;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// The reload button on the table and in the fight
[Collection(ApiCollection.Name)]
public class TableReloadApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private static JsonNode WeaponOf(JsonNode fight, string combatantId, Func<JsonNode, bool> pick) =>
        fight["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == combatantId)!["weapons"]!.AsArray().First(w => pick(w!))!;

    private static bool HasMagazine(JsonNode w) => w["magazine"] is JsonValue;

    // A Solo whose first gun is empty: starting weapons come unloaded, with the ammo in the inventory
    private async Task<(TableScenario.Fight Fight, string Gun, int Magazine)> PcWithEmptyGun(string name)
    {
        var s = await _t.FightOnTheTable(name);
        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        var gun = WeaponOf(fight, s.PcCombatantId, HasMagazine);
        gun["rounds"]!.GetValue<int>().Should().BeLessThan(gun["magazine"]!.GetValue<int>());
        return (s, gun["key"]!.GetValue<string>(), gun["magazine"]!.GetValue<int>());
    }

    [Fact]
    public async Task Pc_reloads_from_inventory_in_fight()
    {
        var (s, gun, magazine) = await PcWithEmptyGun("RecargaPc");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);

        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/reload",
            new { version = await _t.VersionOf(s.EncounterId), weapon = gun });

        WeaponOf(fight, s.PcCombatantId, w => w["key"]!.GetValue<string>() == gun)["rounds"]!.GetValue<int>().Should().Be(magazine);
        fight["log"]!.AsArray().Last()!["text"]!.GetValue<string>().Should().Contain("recarrega");
    }

    // Falco (Edgerunners Mission Kit) carries a Burya with a 4-round magazine; generic enemies have no magazine
    private async Task<(TableScenario.Fight Fight, string Falco, string Gun, int Magazine)> WithFalco(string name)
    {
        var s = await _t.FightOnTheTable(name);
        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants",
            new { version = await _t.VersionOf(s.EncounterId), side = "Enemy", compendiumNpcId = "falco" });
        var falco = fight["queue"]!.AsArray().First(f => f!["name"]!.GetValue<string>().StartsWith("Falco"))!["id"]!.GetValue<string>();
        var gun = WeaponOf(fight, falco, HasMagazine);
        return (s, falco, gun["key"]!.GetValue<string>(), gun["magazine"]!.GetValue<int>());
    }

    [Fact]
    public async Task Npc_reload_refills_magazine()
    {
        var (s, falco, gun, magazine) = await WithFalco("RecargaNpc");
        await _t.MakeActive(s.EncounterId, falco);
        // One shot at the PC takes a round from Falco's magazine.
        await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/attack",
            new { version = await _t.VersionOf(s.EncounterId), attackerId = falco, targetIds = new[] { s.PcCombatantId }, weapon = gun, mode = "Single", range = 5 });

        var fight = await _t.Send<JsonNode>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{falco}/reload",
            new { version = await _t.VersionOf(s.EncounterId), weapon = gun });

        WeaponOf(fight, falco, w => w["key"]!.GetValue<string>() == gun)["rounds"]!.GetValue<int>().Should().Be(magazine);
    }

    [Fact]
    public async Task Player_reload_out_of_turn_is_409()
    {
        var (s, gun, _) = await PcWithEmptyGun("RecargaForaDoTurno");
        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);

        var response = await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/reload", new { weapon = gun }, s.Seat.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Player_reloads_on_own_turn()
    {
        var (s, gun, magazine) = await PcWithEmptyGun("RecargaNoTurno");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);

        var result = await _t.Send<PlayerReloadResult>(_t.Player, HttpMethod.Post, "/api/table/play/reload", new { weapon = gun }, s.Seat.Token);

        result.Encounter!.Queue.Single(f => f.Own).Weapons!.Single(w => w.Key == gun).Rounds.Should().Be(magazine);
    }

    [Fact]
    public async Task Full_magazine_is_400()
    {
        var (s, falco, gun, _) = await WithFalco("PenteCheio");

        var response = await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{falco}/reload",
            new { version = await _t.VersionOf(s.EncounterId), weapon = gun });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_reload_outside_fight_works()
    {
        var solo = await _t.FinalizedSolo("RecargaLivre");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Livre", solo.Id), expected: HttpStatusCode.Created);
        var gun = solo.Report.Weapons.First(w => w.Magazine is not null && w.LoadedRounds < w.Magazine);

        var response = await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/reload", new { weapon = gun.InstanceId }, seat.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{solo.Id}")).Report.Weapons.Single(w => w.InstanceId == gun.InstanceId)
            .LoadedRounds.Should().Be(gun.Magazine);
    }
}
