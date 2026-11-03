using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table.Dice;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Table;

public class TableDiceTests
{
    private sealed class FixedDice(params int[] rolls) : IDiceRoller
    {
        private int _i;
        public int Roll(int sides) => rolls[_i++ % rolls.Length];
    }

    [Theory]
    [InlineData("1d10+12", 2)]
    [InlineData("3d6", 1)]
    [InlineData("2d6 + 1d6 - 2", 3)]
    [InlineData("d10", 1)]
    [InlineData("15", 1)]
    [InlineData("1D100", 1)]
    public void Formulas_split_into_terms(string formula, int terms) =>
        DiceFormula.Parse(formula).Terms.Should().HaveCount(terms);

    [Theory]
    [InlineData("")]
    [InlineData("1d")]
    [InlineData("abc")]
    [InlineData("1d10++2")]
    [InlineData("0d6")]
    [InlineData("1d1")]
    [InlineData("101d6")]
    [InlineData("1d10001")]
    [InlineData("1d6; drop table")]
    public void Nonsense_is_refused(string formula) =>
        FluentActions.Invoking(() => DiceFormula.Parse(formula)).Should().Throw<RequestValidationException>();

    [Fact]
    public void A_plain_roll_adds_dice_and_modifiers()
    {
        var roll = TableRoller.Roll("2d6+1d6-2", RollKind.Plain, new FixedDice(3, 4, 5));
        roll.Total.Should().Be(3 + 4 + 5 - 2);
        roll.Terms.Should().HaveCount(3);
        roll.Terms[0].Dice.Should().Equal(3, 4);
        roll.Terms[2].Value.Should().Be(-2);
        roll.Critical.Should().BeNull();
        roll.CriticalInjury.Should().BeFalse();
    }

    [Fact]
    public void A_natural_10_on_a_check_adds_another_d10()
    {
        var roll = TableRoller.Roll("1d10+12", RollKind.Check, new FixedDice(10, 7));
        roll.Total.Should().Be(10 + 7 + 12);
        roll.Critical.Should().Be(CriticalResult.Success);
        roll.Terms[0].Extra.Should().Be(7);
    }

    [Fact]
    public void A_natural_1_on_a_check_subtracts_another_d10()
    {
        var roll = TableRoller.Roll("1d10+12", RollKind.Check, new FixedDice(1, 6));
        roll.Total.Should().Be(1 - 6 + 12);
        roll.Critical.Should().Be(CriticalResult.Failure);
        roll.Terms[0].Extra.Should().Be(-6);
    }

    [Fact]
    public void The_extra_d10_does_not_chain()
    {
        var roll = TableRoller.Roll("1d10", RollKind.Check, new FixedDice(10, 10, 10));
        roll.Total.Should().Be(20);
    }

    [Fact]
    public void Only_a_single_d10_check_criticals()
    {
        TableRoller.Roll("1d10+5", RollKind.Plain, new FixedDice(10, 9)).Total.Should().Be(15);
        FluentActions.Invoking(() => TableRoller.Roll("2d6+3", RollKind.Check, new FixedDice(1)))
            .Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Two_sixes_on_damage_warn_of_a_critical_injury()
    {
        TableRoller.Roll("3d6", RollKind.Damage, new FixedDice(6, 2, 6)).CriticalInjury.Should().BeTrue();
        TableRoller.Roll("3d6", RollKind.Damage, new FixedDice(6, 2, 5)).CriticalInjury.Should().BeFalse();
        TableRoller.Roll("2d6", RollKind.Plain, new FixedDice(6, 6)).CriticalInjury.Should().BeFalse();
        // Only the d6s count: a 6 on another die is not part of it.
        TableRoller.Roll("1d10+1d6", RollKind.Damage, new FixedDice(6, 6)).CriticalInjury.Should().BeFalse();
    }
}
