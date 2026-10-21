using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CombatOperationsTests
{
    private sealed class FixedDice(params int[] rolls) : IDiceRoller
    {
        private int _i;
        public int Roll(int sides) => rolls[_i++ % rolls.Length];
    }

    private static readonly CombatOperations Combat = new(Rules, Calculator, new FixedDice(5));

    /// <summary>Finalized Complete Package Solo (45 HP, BODY 7) wearing Light Armorjack on body and head.</summary>
    private static CharacterSheet Armored() => CompletePackage() with
    {
        Creation = CompletePackage().Creation with { Finalized = true },
        Inventory =
        [
            new InventoryEntry { InstanceId = "b", ItemId = LightArmorjack.Id, State = ItemState.Equipped, Worn = ArmorSlot.Body },
            new InventoryEntry { InstanceId = "h", ItemId = LightArmorjack.Id, State = ItemState.Equipped, Worn = ArmorSlot.Head },
        ],
    };

    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, DateTime.UtcNow);
    private static CharacterReport Calc(CharacterSheet s) => Calculator.Calculate(s, Catalog);

    [Fact]
    public void Damage_through_the_head_is_doubled_and_tripled_with_cracked_skull()
    {
        var head = Combat.Damage(Armored(), new DamageRequest(0, 15, InjuryLocation.Head), Change(Armored()));
        Calc(head).Health.CurrentHp.Should().Be(45 - 8);
        head.Inventory.Single(i => i.InstanceId == "h").CurrentSp.Should().Be(10);
        head.Inventory.Single(i => i.InstanceId == "b").CurrentSp.Should().BeNull("só a armadura do local atingido é ablada");

        var cracked = Armored() with { Injuries = [new CriticalInjuryRecord { Id = "c", Name = "Cracked Skull", Location = InjuryLocation.Head }] };
        Calc(Combat.Damage(cracked, new DamageRequest(0, 15, InjuryLocation.Head), Change(cracked))).Health.CurrentHp.Should().Be(45 - 12);
    }

    [Fact]
    public void Damage_stopped_by_armor_does_not_ablate_it()
    {
        var sheet = Combat.Damage(Armored(), new DamageRequest(0, 11), Change(Armored()));
        Calc(sheet).Health.CurrentHp.Should().Be(45);
        sheet.Inventory.Single(i => i.InstanceId == "b").CurrentSp.Should().BeNull();
    }

    [Fact]
    public void Armor_implants_ablate_together_with_worn_armor()
    {
        var sheet = Armored() with { Cyberware = [new CharacterCyberware { InstanceId = "sd", ItemId = SubdermalArmor.Id, HumanityLossApplied = 14 }] };
        var hit = Combat.Damage(sheet, new DamageRequest(0, 20), Change(sheet));
        hit.Cyberware.Single().CurrentSp.Should().Be(10);
        hit.Inventory.Single(i => i.InstanceId == "b").CurrentSp.Should().Be(10);
    }

    [Fact]
    public void Damage_while_mortally_wounded_raises_the_death_save_penalty_and_asks_for_an_injury()
    {
        var dying = Armored() with { Combat = new CombatState { CurrentHp = 0, Stabilized = false } };
        var change = Change(dying);
        var hit = Combat.Damage(dying, new DamageRequest(0, 5, BypassArmor: true), change);

        hit.Combat.DeathSavePenalty.Should().Be(1);
        change.Notices.Should().Contain(n => n.Contains("Critical Injury"));
    }

    [Fact]
    public void A_natural_ten_always_fails_the_death_save_and_every_save_raises_the_penalty()
    {
        var dying = Armored() with { Stats = new Dictionary<string, int>(Armored().Stats) { ["BODY"] = 10 }, Combat = new CombatState { CurrentHp = 0 } };

        var survived = Combat.DeathSave(dying, new DeathSaveRequest(0, 9), Change(dying));
        survived.Combat.Conditions.Should().NotContain("Morto");
        survived.Combat.DeathSavePenalty.Should().Be(1);

        var failed = Combat.DeathSave(dying, new DeathSaveRequest(0, 10), Change(dying));
        failed.Combat.Conditions.Should().Contain("Morto");

        var penalized = Combat.DeathSave(survived, new DeathSaveRequest(0, 9), Change(survived));
        penalized.Combat.Conditions.Should().Contain("Morto", "9 + 1 = 10 não fica abaixo de BODY 10");
    }

    [Fact]
    public void Death_saves_are_refused_when_not_mortally_wounded()
    {
        var act = () => Combat.DeathSave(Armored(), new DeathSaveRequest(0, 1), Change(Armored()));
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Injuries_without_quick_fix_can_only_be_treated()
    {
        var sheet = Combat.AddInjury(Armored(), new AddInjuryRequest(0, InjuryLocation.Head, "Lost Eye"), Change(Armored()));
        Calc(sheet).Health.CurrentHp.Should().Be(40, "5 de dano bônus direto no HP");
        Calc(sheet).Health.BaseDeathSavePenalty.Should().Be(1);

        var id = sheet.Injuries.Single().Id;
        var quickFix = () => Combat.UpdateInjury(sheet, id, new UpdateInjuryRequest(0, InjuryStatus.QuickFixed), Change(sheet));
        quickFix.Should().Throw<RequestValidationException>();

        var duplicate = () => Combat.AddInjury(sheet, new AddInjuryRequest(0, InjuryLocation.Head, "Lost Eye"), Change(sheet));
        duplicate.Should().Throw<RequestValidationException>();

        var treated = Combat.UpdateInjury(sheet, id, new UpdateInjuryRequest(0, InjuryStatus.Healed, "Surgery DV17"), Change(sheet));
        Calc(treated).Health.BaseDeathSavePenalty.Should().Be(0);
    }

    [Fact]
    public void Quick_fix_removes_some_injuries_for_good()
    {
        var sheet = Combat.AddInjury(Armored(), new AddInjuryRequest(0, InjuryLocation.Head, "Concussion", ApplyBonusDamage: false), Change(Armored()));
        var fixedSheet = Combat.UpdateInjury(sheet, sheet.Injuries.Single().Id, new UpdateInjuryRequest(0, InjuryStatus.QuickFixed), Change(sheet));
        fixedSheet.Injuries.Single().Status.Should().Be(InjuryStatus.Healed);
    }

    [Theory]
    [InlineData("2d6", new[] { 3, 4 }, 7)]
    [InlineData("1d6/2", new[] { 5 }, 3)]
    [InlineData("4d6", new[] { 1, 2, 3, 4 }, 10)]
    public void Dice_expressions_follow_the_humanity_loss_notation(string expression, int[] rolls, int expected)
    {
        Dice.Roll(expression, new FixedDice(rolls)).Should().Be(expected);
        Dice.Roll("N/A", new FixedDice(1)).Should().BeNull();
    }
}
