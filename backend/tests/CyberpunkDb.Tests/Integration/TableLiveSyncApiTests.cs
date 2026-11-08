using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace CyberpunkDb.Tests.Integration;

// Changes made on a character sheet reach the table at once
[Collection(ApiCollection.Name)]
public class TableLiveSyncApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    [Fact]
    public async Task Equipping_armor_pushes_encounter_to_seats()
    {
        var s = await _t.FightOnTheTable("Sincronia");
        var sheet = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        var body = sheet.Report.Armor.Body.InstanceId!;

        await using var player = TableFoundationApiTests.Hub(factory, s.Seat.Token, playerPort: true);
        var fights = new List<PlayerEncounterDto?>();
        var got = new SemaphoreSlim(0);
        player.On<PlayerEncounterDto?>("encounter", f => { lock (fights) fights.Add(f); got.Release(); });
        await player.StartAsync();
        await Task.Delay(300); // long polling: let the connection join its groups

        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/equip",
            new EquipRequest(sheet.Version, body, ItemState.Stored));

        (await got.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("the seat must hear about the armor coming off");
        lock (fights) fights.Last()!.Queue.Single(f => f.Own).SpBody.Should().Be(0);
    }

    [Fact]
    public async Task Encounter_attack_broadcasts_once()
    {
        var s = await _t.FightOnTheTable("SemEco");
        await using var player = TableFoundationApiTests.Hub(factory, s.Seat.Token, playerPort: true);
        var count = 0;
        player.On<JsonElement>("encounter", _ => Interlocked.Increment(ref count));
        await player.StartAsync();
        await Task.Delay(300);

        // Damage to the PC through the fight changes the sheet inside the fight's own change: one push, not two.
        await _t.Send<JsonElement>(_t.Gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{s.PcCombatantId}/damage",
            new { version = await _t.VersionOf(s.EncounterId), amount = 5, location = "Body" });

        await Task.Delay(1000);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Sheet_change_without_fight_sends_party_only()
    {
        var solo = await _t.FinalizedSolo("SemLuta");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Sem luta", solo.Id), expected: System.Net.HttpStatusCode.Created);
        await using var player = TableFoundationApiTests.Hub(factory, seat.Token, playerPort: true);
        var party = new SemaphoreSlim(0);
        var fights = 0;
        player.On<JsonElement>("party", _ => party.Release());
        player.On<JsonElement>("encounter", _ => Interlocked.Increment(ref fights));
        await player.StartAsync();
        await Task.Delay(300);

        await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{solo.Id}/luck", new LuckRequest(solo.Version, 1));

        (await party.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        fights.Should().Be(0);
    }
}
