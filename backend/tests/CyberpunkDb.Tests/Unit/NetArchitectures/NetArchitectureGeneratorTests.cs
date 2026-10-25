using System.Text.Json;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NetArchitectures;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NetArchitectures.NetTestData;

namespace CyberpunkDb.Tests.Unit.NetArchitectures;

public class NetArchitectureGeneratorTests
{
    private static IEnumerable<ArchitectureState> Many(Func<int, GenerateArchitectureRequest>? request = null) =>
        Enumerable.Range(1, 200).Select(seed => Generate(request?.Invoke(seed) ?? new GenerateArchitectureRequest { Seed = seed }));

    [Fact]
    public void Shape_follows_the_core_book()
    {
        foreach (var s in Many())
        {
            s.FloorCount.Should().BeInRange(3, 18);
            var main = s.Main;
            main.Floors.Select(f => f.Depth).Should().Equal(Enumerable.Range(1, main.Floors.Count));
            main.Floors.Take(2).Should().OnlyContain(f => f.Table == "Lobby", "os dois primeiros andares são o Lobby");
            main.Floors.Skip(2).Should().OnlyContain(f => f.Table == s.Difficulty.ToString());
            foreach (var b in s.Branches.Skip(1))
            {
                b.FromFloor.Should().BeGreaterThanOrEqualTo(2, "nenhum ramo começa antes do terceiro andar");
                b.Floors.Should().NotBeEmpty();
                b.Floors[0].Depth.Should().Be(b.FromFloor!.Value + 1);
                b.Floors[^1].Depth.Should().BeLessThan(main.Floors.Count, "o tronco é o único caminho até o fundo");
                b.Floors.Should().OnlyContain(f => f.Table == s.Difficulty.ToString());
            }
        }
    }

    [Fact]
    public void Black_ice_and_password_rows_never_repeat_and_dvs_follow_the_difficulty()
    {
        foreach (var s in Many())
        {
            foreach (var group in s.Floors.GroupBy(f => f.Table))
            {
                var unique = group.Where(f => Rules.Unique(Rules.Row(f.Table, f.Roll!.Value))).Select(f => f.Roll).ToList();
                unique.Should().OnlyHaveUniqueItems($"seed {s.Seed}, tabela {group.Key}");
            }
            var dv = Rules.Difficulty(s.Difficulty).Dv;
            s.Floors.Where(f => f.Table != "Lobby").SelectMany(f => f.Contents).Where(c => c.Dv is not null && c.Dv != dv).Should().BeEmpty();
        }
    }

    [Fact]
    public void Control_nodes_respect_the_purchase_limit_and_every_one_has_a_purpose()
    {
        foreach (var s in Many())
        {
            var nodes = s.Contents.Where(c => c.Kind == NetContentKind.ControlNode).ToList();
            if (Rules.Band(s.FloorCount).MaxControlNodes is { } max) nodes.Count.Should().BeLessThanOrEqualTo(max, $"seed {s.Seed}");
            nodes.Where(n => n.Function?.DefenseId is null && n.Function?.DeviceId is null).Should().BeEmpty();
            s.Contents.Where(c => c.Kind == NetContentKind.File && c.FileIdea is null).Should().BeEmpty();
        }
    }

    [Fact]
    public void Active_defenses_always_have_a_demon_within_the_limit()
    {
        foreach (var s in Many(seed => new GenerateArchitectureRequest { Seed = seed, LocationId = "military", Floors = 3 + seed % 16 }))
        {
            s.Demons.Count.Should().BeLessThanOrEqualTo(Rules.MaxDemons(s.FloorCount));
            foreach (var node in s.Contents.Where(c => Rules.FindDefense(c.Function?.DefenseId)?.RequiresDemon == true))
                s.Demons.Should().Contain(d => d.Id == node.Function!.DemonId, $"seed {s.Seed}");
            s.Demons.Where(d => s.Floor(d.FloorId) is null).Should().BeEmpty();
            Pricing.Quote(s, Ice).Warnings.Should().BeEmpty($"seed {s.Seed}");
        }
    }

    [Fact]
    public void Defense_loadouts_take_the_number_of_options_the_book_gives()
    {
        foreach (var node in Many(seed => new GenerateArchitectureRequest { Seed = seed, LocationId = "military" }).SelectMany(s => s.Contents))
            if (Rules.FindDefense(node.Function?.DefenseId) is { } d)
                {
                node.Function!.Loadout.Should().HaveCount(d.LoadoutPicks);
                node.Function.Loadout.Where(l => !d.LoadoutOptions.Contains(l)).Should().BeEmpty();
            }
    }

    [Fact]
    public void Same_seed_gives_the_same_architecture()
    {
        var request = new GenerateArchitectureRequest { Seed = 2045, LocationId = "corporate-office" };
        JsonSerializer.Serialize(Generate(request)).Should().Be(JsonSerializer.Serialize(Generate(request)));
    }

    [Fact]
    public void Chosen_options_are_kept()
    {
        var s = Generate(new GenerateArchitectureRequest { Seed = 7, Difficulty = NetDifficulty.Advanced, Floors = 12, Branches = 2, EnemyNetrunner = true });

        s.Difficulty.Should().Be(NetDifficulty.Advanced);
        s.FloorCount.Should().Be(12);
        s.Branches.Should().HaveCount(3);
        var bottom = s.Main.Floors.Last().Id;
        var netrunner = s.Netrunners.Should().ContainSingle().Which;
        netrunner.Interface.Should().Be(8, "o Interface 'com chance' da dificuldade Avançada");
        netrunner.FloorId.Should().Be(bottom, "Netrunners ficam no andar mais fundo (GQ p.5)");
    }

    [Fact]
    public void Too_few_floors_drop_the_branches_that_do_not_fit()
    {
        Generate(new GenerateArchitectureRequest { Seed = 1, Floors = 3, Branches = 3 }).Branches.Should().ContainSingle();
        Generate(new GenerateArchitectureRequest { Seed = 1, Floors = 5, Branches = 3 }).Branches.Should().HaveCount(2);
    }

    [Fact]
    public void Floor_count_outside_the_dice_is_rejected()
    {
        var act = () => Generate(new GenerateArchitectureRequest { Floors = 19 });
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Refill_keeps_locked_floors()
    {
        var s = Generate(new GenerateArchitectureRequest { Seed = 11, Floors = 10 });
        Operations.LockFloor(s, "A5", true);
        var locked = JsonSerializer.Serialize(s.Floor("A5"));

        Generator.Refill(s, new Random(99));

        JsonSerializer.Serialize(s.Floor("A5")).Should().Be(locked);
        s.FloorCount.Should().Be(10);
    }
}
