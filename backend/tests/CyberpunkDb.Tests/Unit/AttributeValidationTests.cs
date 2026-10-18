using System.Text.Json;
using CyberpunkDb.Application.Catalog.Categories;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit;

public class AttributeValidationTests
{
    private static Dictionary<string, JsonElement> Attrs(object values) =>
        JsonSerializer.SerializeToElement(values).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    [Fact]
    public void Clothing_cannot_receive_stopping_power()
    {
        var def = new ArmorCategory();
        var item = def.CreateEntity();
        var errors = def.ApplyAttributes(item, Attrs(new { armorType = "Clothing", clothingSlot = "Top", sp = 11 }), isCreate: true);
        errors.Should().ContainKey("attributes.sp");
    }

    [Fact]
    public void Body_armor_accepts_stopping_power_and_penalties()
    {
        var def = new ArmorCategory();
        var item = (ArmorItem)def.CreateEntity();
        var errors = def.ApplyAttributes(item, Attrs(new { armorType = "BodyArmor", sp = 13, refPenalty = 2 }), isCreate: true);
        errors.Should().BeEmpty();
        item.StoppingPower.Should().Be(13);
        item.ArmorType.Should().Be(ArmorType.BodyArmor);
    }

    [Fact]
    public void Linear_frame_requires_body_value()
    {
        var def = new ArmorCategory();
        var errors = def.ApplyAttributes(def.CreateEntity(), Attrs(new { armorType = "LinearFrame" }), isCreate: true);
        errors.Should().ContainKey("attributes.bodyValue");
    }

    [Fact]
    public void Required_subtype_unknown_attributes_and_out_of_range_values_are_reported()
    {
        var def = new WeaponCategory();
        var errors = def.ApplyAttributes(def.CreateEntity(), Attrs(new { rof = 99, laser = true }), isCreate: true);
        errors.Keys.Should().Contain(["attributes.weaponClass", "attributes.rof", "attributes.laser"]);
    }

    [Fact]
    public void Melee_weapons_cannot_have_ammunition()
    {
        var def = new WeaponCategory();
        var errors = def.ApplyAttributes(def.CreateEntity(),
            Attrs(new { weaponClass = "VeryHeavyMelee", calibers = new[] { "Rifle" }, magazine = 10 }), isCreate: true);
        errors.Keys.Should().Contain(["attributes.calibers", "attributes.magazine"]);
    }

    [Fact]
    public void Collections_are_updated_in_place()
    {
        var def = new WeaponCategory();
        var weapon = new Weapon { WeaponClass = WeaponClass.Shotgun, Calibers = [new WeaponCaliber { WeaponId = 7, Caliber = Caliber.Slug }] };
        var original = weapon.Calibers[0];
        def.ApplyAttributes(weapon, Attrs(new { calibers = new[] { "Slug", "ShotgunShell" } }), isCreate: false).Should().BeEmpty();
        weapon.Calibers.Select(c => c.Caliber).Should().Equal(Caliber.Slug, Caliber.ShotgunShell);
        weapon.Calibers[0].Should().BeSameAs(original, "existing rows must be kept so EF does not re-insert them");
    }

    [Theory]
    [InlineData("\"Sim\"", typeof(bool), true)]
    [InlineData("\"nao\"", typeof(bool), false)]
    [InlineData("\"12,5\"", typeof(decimal), 12.5)]
    [InlineData("\"Rifle de Assalto\"", typeof(WeaponClass), WeaponClass.AssaultRifle)]
    [InlineData("\"assaultrifle\"", typeof(WeaponClass), WeaponClass.AssaultRifle)]
    public void Converter_accepts_friendly_input(string json, Type type, object expected)
    {
        var value = FieldValueConverter.Convert(JsonDocument.Parse(json).RootElement, type, "f");
        value.Should().Be(type == typeof(decimal) ? Convert.ToDecimal(expected) : expected);
    }

    [Fact]
    public void Converter_rejects_numeric_strings_for_enums()
    {
        var act = () => FieldValueConverter.Convert(JsonDocument.Parse("\"3\"").RootElement, typeof(WeaponClass), "f");
        act.Should().Throw<CyberpunkDb.Application.Common.RequestValidationException>();
    }
}
