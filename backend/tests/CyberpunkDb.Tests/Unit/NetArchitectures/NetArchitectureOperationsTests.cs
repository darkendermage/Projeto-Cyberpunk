using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NetArchitectures;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NetArchitectures.NetTestData;

namespace CyberpunkDb.Tests.Unit.NetArchitectures;

public class NetArchitectureOperationsTests
{
    private static ArchitectureState Fresh(int floors = 9) =>
        Generate(new GenerateArchitectureRequest { Seed = 42, Floors = floors, Branches = 0, Difficulty = NetDifficulty.Standard, EnemyNetrunner = false });

    [Fact]
    public void Boss_floor_comes_from_one_difficulty_up()
    {
        var s = Fresh();
        Operations.SetFloor(s, "A5", "Uncommon", 18, new Random(1));
        s.Floor("A5")!.Contents.Single().Slug.Should().Be("giant");

        var act = () => Operations.SetFloor(s, "A5", "Advanced", 18, new Random(1));
        act.Should().Throw<RequestValidationException>();
        var lobby = () => Operations.SetFloor(s, "A1", "Standard", 9, new Random(1));
        lobby.Should().Throw<RequestValidationException>("o Lobby usa só a tabela do Lobby");
    }

    [Fact]
    public void Black_ice_can_only_be_swapped_for_one_of_the_same_cost()
    {
        var s = Fresh();
        Operations.SetFloor(s, "A4", "Standard", 8, new Random(1));   // Hellhound, 500eb
        var id = s.Floor("A4")!.Contents.Single().Id;

        Operations.SwapIce(s, id, "liche", Ice);                      // Liche, 500eb
        s.Floor("A4")!.Contents.Single().Slug.Should().Be("liche");

        var act = () => Operations.SwapIce(s, id, "giant", Ice);       // Giant, 1,000eb
        act.Should().Throw<RequestValidationException>().Which.Errors["slug"].Single().Should().Contain("mesmo custo");
    }

    [Fact]
    public void Active_defense_needs_a_demon()
    {
        var s = Fresh();
        Operations.SetFloor(s, "A4", "Standard", 11, new Random(1));
        var node = s.Floor("A4")!.Contents.Single().Id;

        var act = () => Operations.SetNode(s, node, new SetNodeRequest(1, "mini-air-drone", ["Câmera de observação"], null, null, null));
        act.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("demonId");

        if (s.Demons.Count == 0) Operations.AddDemon(s, new DemonRequest(1, "imp", "A4"));
        Operations.SetNode(s, node, new SetNodeRequest(1, "mini-air-drone", ["Câmera de observação"], null, null, s.Demons[0].Id));
        s.Floor("A4")!.Contents.Single().Function!.DefenseId.Should().Be("mini-air-drone");

        var wrongLoadout = () => Operations.SetNode(s, node, new SetNodeRequest(1, "automated-turret", [], null, null, null));
        wrongLoadout.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("loadout");
    }

    [Fact]
    public void Demons_stay_within_one_per_six_floors()
    {
        var s = Fresh(floors: 6);
        while (s.Demons.Count < Rules.MaxDemons(6)) Operations.AddDemon(s, new DemonRequest(1, "imp", null));

        var act = () => Operations.AddDemon(s, new DemonRequest(1, "efreet", null));
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Removing_the_only_demon_of_an_active_defense_is_refused()
    {
        var s = Fresh();
        Operations.SetFloor(s, "A4", "Standard", 11, new Random(1));
        if (s.Demons.Count == 0) Operations.AddDemon(s, new DemonRequest(1, "imp", null));
        foreach (var extra in s.Demons.Skip(1).ToList()) Operations.RemoveDemon(s, extra.Id);
        var node = s.Floor("A4")!.Contents.Single().Id;
        Operations.SetNode(s, node, new SetNodeRequest(1, "ground-drone", ["Câmera de observação", "SMG com 30 balas Basic"], null, null, s.Demons[0].Id));

        var act = () => Operations.RemoveDemon(s, s.Demons[0].Id);
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Locked_floor_is_not_rerolled_and_netrunners_can_be_managed()
    {
        var s = Fresh();
        Operations.LockFloor(s, "A6", true);
        var act = () => Operations.RerollFloor(s, "A6", new Random(1));
        act.Should().Throw<RequestValidationException>();

        Operations.AddNetrunner(s, new NetrunnerRequest(1, "Sentinela", 6, 2, null));
        s.Netrunners.Should().ContainSingle().Which.FloorId.Should().Be(s.Main.Floors[^1].Id);
        Operations.UpdateNetrunner(s, "N1", new NetrunnerRequest(1, "Sentinela", 7, 0, "A3"));
        s.Netrunners.Single().Interface.Should().Be(7);
        Operations.RemoveNetrunner(s, "N1");
        s.Netrunners.Should().BeEmpty();
    }

    [Fact]
    public void Export_uses_the_core_book_table_layout()
    {
        var s = Fresh();
        Operations.SetFloor(s, "A3", "Standard", 9, new Random(1));
        var text = Export.Render("Teste", s, Pricing.Quote(s, Ice), Ice, markdown: false);

        text.Should().MatchRegex(@"Floor\s+\| On the Other Side of the ""Door""\s+\| DV");
        text.Should().Contain("(Backdoor DV8)");
        text.Should().Contain("Total:");
        Export.Render("Teste", s, Pricing.Quote(s, Ice), Ice, markdown: true).Should().Contain("|---|---|---|");
    }
}
