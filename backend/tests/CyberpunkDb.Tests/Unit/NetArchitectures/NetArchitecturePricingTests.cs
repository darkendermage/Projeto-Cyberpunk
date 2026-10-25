using CyberpunkDb.Application.NetArchitectures;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NetArchitectures.NetTestData;

namespace CyberpunkDb.Tests.Unit.NetArchitectures;

public class NetArchitecturePricingTests
{
    /// <summary>CP:R p.218, "Home Security: An Example": TLB LTD's 4-floor warehouse system costs 13,500eb.</summary>
    private static ArchitectureState TlbLtd() => new()
    {
        Difficulty = NetDifficulty.Standard,
        Branches =
        [
            new NetBranch
            {
                Id = "A",
                Floors =
                [
                    Floor("A", 1, IceContent("hellhound"), IceContent("killer")),
                    Floor("A", 2, Feature(NetContentKind.Password, 8)),
                    Floor("A", 3, IceContent("hellhound")),
                    Floor("A", 4, Feature(NetContentKind.ControlNode, 8, new NodeFunction { DefenseId = "automated-turret", Loadout = ["Assault Rifle com 25 balas Basic"] })),
                ],
            },
        ],
    };

    [Fact]
    public void Core_book_example_costs_13500()
    {
        var quote = Pricing.Quote(TlbLtd(), Ice);

        quote.Total.Should().Be(13_500);
        quote.Lines.Single(l => l.Group == "Andares").Total.Should().Be(4_000);
        quote.Lines.Where(l => l.Label.StartsWith("A1")).Select(l => l.Total).Should().Equal(1_000, 1_000);
        quote.Lines.Single(l => l.Group == "Defesas").Total.Should().Be(5_000);
        quote.Portable.Should().BeTrue();
        quote.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Three_black_ice_on_a_floor_cost_triple_each()
    {
        var state = TlbLtd();
        state.Main.Floors[0] = Floor("A", 1, IceContent("wisp"), IceContent("wisp"), IceContent("wisp"));

        Pricing.Quote(state, Ice).Lines.Where(l => l.Label.StartsWith("A1")).Should().OnlyContain(l => l.Total == 150, "Wisp custa 50eb, ×3 no mesmo andar");
    }

    [Fact]
    public void Floor_price_follows_the_band()
    {
        var s = Generate(new GenerateArchitectureRequest { Seed = 3, Floors = 13 });
        Pricing.Quote(s, Ice).Lines.Single(l => l.Group == "Andares").Total.Should().Be(130_000);
    }

    [Fact]
    public void Breaking_the_rules_shows_warnings()
    {
        var state = TlbLtd();
        state.Main.Floors[2] = Floor("A", 3, Feature(NetContentKind.ControlNode, 8, new NodeFunction { DefenseId = "ground-drone", Loadout = ["Câmera de observação", "SMG com 30 balas Basic"] }));
        state.Main.Floors[1] = Floor("A", 2, Feature(NetContentKind.ControlNode, 8, new NodeFunction { DeviceId = "lighting" }));

        var warnings = Pricing.Quote(state, Ice).Warnings;

        warnings.Should().Contain(w => w.Contains("limite de 2"), "3 Control Nodes em 4 andares passam do limite portátil");
        warnings.Should().Contain(w => w.Contains("Ground Drone") && w.Contains("Demon"));
    }
}
