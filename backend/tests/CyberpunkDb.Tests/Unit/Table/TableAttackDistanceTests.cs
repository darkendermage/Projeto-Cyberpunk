using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Table;

// The distance of a quick attack, measured between tokens like the ruler of the map (grid.ts).
public class TableAttackDistanceTests
{
    private static SceneToken At(double x, double y, double size = 1) => new() { X = x, Y = y, Width = size, Height = size };

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 3, 0, 6)]
    [InlineData(0, 0, 3, 4, 8)] // a diagonal step counts as one square
    [InlineData(2, 2, 2, 7, 10)]
    public void Squares_between_the_cells_of_the_token_centers(double ax, double ay, double bx, double by, double meters) =>
        TableAttacks.Meters(new SceneGrid { MetersPerCell = 2 }, At(ax, ay), At(bx, by)).Should().Be(meters);

    [Fact]
    public void A_big_token_measures_from_its_center() =>
        TableAttacks.Meters(new SceneGrid { MetersPerCell = 2 }, At(0, 0, 2), At(4, 1)).Should().Be(6);

    [Fact]
    public void Without_a_grid_it_is_the_straight_line() =>
        TableAttacks.Meters(new SceneGrid { Type = GridType.None, MetersPerCell = 2 }, At(0, 0), At(3, 4)).Should().Be(10);
}
