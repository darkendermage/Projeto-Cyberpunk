using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// Chipware Sockets that repeat, implant armor per location, and the weapon inside a popup
[Collection(ApiCollection.Name)]
public class CyberwareFixesApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<int> ItemId(string slug) =>
        (await _t.Gm.GetFromJsonAsync<JsonNode>($"/api/items/by-slug/{slug}"))!["item"]!["id"]!.GetValue<int>();

    private async Task<CharacterDetailDto> Rich(string name)
    {
        var c = await _t.FinalizedSolo(name);
        return await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/balance", new AdjustBalanceRequest(c.Version, 50_000, "teste"));
    }

    private Task<CharacterDetailDto> Install(CharacterDetailDto c, int itemId) =>
        _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/cyberware/install", new InstallCyberwareRequest(c.Version, itemId, Acknowledge: true));

    [Fact]
    public async Task A_second_chip_gets_its_own_socket_on_the_same_Neural_Link()
    {
        var c = await Rich("Chips");
        var chip = await ItemId("skill-chip");

        c = await Install(c, chip);
        c = await Install(c, chip);
        c = await Install(c, await ItemId("pain-editor"));

        var lines = c.Report.Cyberware;
        lines.Count(l => l.Slug == "neural-link").Should().Be(1);
        var sockets = lines.Where(l => l.Slug == "chipware-socket").ToList();
        sockets.Should().HaveCount(3, "each socket holds a single chip (CP:R p.359)");
        sockets.Select(s => s.ParentInstanceId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Implant_armor_ablates_only_the_location_hit_and_repairs_per_location()
    {
        var c = await Install(await Rich("Subdermal"), await ItemId("subdermal-armor"));
        c.Report.Armor.Head.Sp.Should().Be(11);
        c.Report.Armor.Body.Sp.Should().Be(11);

        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/armor/ablate", new AblateRequest(c.Version, ArmorSlot.Body, 2));
        c.Report.Armor.Body.Sp.Should().Be(9);
        c.Report.Armor.Head.Sp.Should().Be(11, "a hit to the body doesn't wear the head plates");

        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/armor/ablate", new AblateRequest(c.Version, ArmorSlot.Head, 1));
        c.Report.Armor.Head.Sp.Should().Be(10);

        var implant = c.Report.Armor.Head.InstanceId!;
        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/armor/repair", new RepairArmorRequest(c.Version, implant, null, ArmorSlot.Head));
        c.Report.Armor.Head.Sp.Should().Be(11);
        c.Report.Armor.Body.Sp.Should().Be(9, "repairing the head leaves the body as it was");
    }

    [Fact]
    public async Task Heavy_Subdermal_Plating_counts_as_armor()
    {
        var c = await Install(await Rich("Plating"), await ItemId("heavy-subdermal-plating"));
        c.Report.Armor.Body.Sp.Should().BeGreaterThanOrEqualTo(13);
        c.Report.Armor.Head.Sp.Should().BeGreaterThanOrEqualTo(13);
    }

    [Fact]
    public async Task A_weapon_put_in_a_Popup_Melee_Weapon_is_always_at_hand()
    {
        var c = await Rich("Popup");
        c = await Install(c, await ItemId("popup-melee-weapon"));
        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/purchase",
            new PurchaseRequest(c.Version, await ItemId("medium-melee-weapon")));
        var popup = c.Report.Cyberware.Single(l => l.Slug == "popup-melee-weapon");
        var blade = c.Report.Inventory.Last(i => i.Slug == "medium-melee-weapon");
        popup.PopupMelee.Should().BeTrue();
        popup.PopupCandidates.Should().Contain(blade.InstanceId);

        c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/cyberware/hosted-weapon",
            new HostWeaponRequest(c.Version, popup.InstanceId, blade.InstanceId));

        var line = c.Report.Weapons.Single(w => w.InstanceId == blade.InstanceId);
        line.Equipped.Should().BeTrue();
        line.HostedIn.Should().Be("Popup Melee Weapon");
        c.Report.Cyberware.Single(l => l.Slug == "popup-melee-weapon").HostedWeaponId.Should().Be(blade.InstanceId);
    }

    [Fact]
    public async Task A_ranged_weapon_does_not_fit_a_Popup_Melee_Weapon()
    {
        var c = await Rich("Popup errado");
        c = await Install(c, await ItemId("popup-melee-weapon"));
        var popup = c.Report.Cyberware.Single(l => l.Slug == "popup-melee-weapon");
        var pistol = c.Report.Weapons.FirstOrDefault(w => w.Calibers.Count > 0);
        if (pistol is null)
        {
            c = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/purchase", new PurchaseRequest(c.Version, await ItemId("medium-pistol")));
            pistol = c.Report.Weapons.First(w => w.Calibers.Count > 0);
        }

        var response = await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/characters/{c.Id}/cyberware/hosted-weapon",
            new HostWeaponRequest(c.Version, popup.InstanceId, pistol.InstanceId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
