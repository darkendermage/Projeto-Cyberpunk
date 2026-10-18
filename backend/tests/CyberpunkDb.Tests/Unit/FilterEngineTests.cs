using System.Text.Json;
using CyberpunkDb.Application.Catalog.Categories;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit;

/// <summary>
/// The predicates built by the field definitions are plain expression trees, so they can be checked
/// against in-memory data with LINQ-to-Objects — the same expressions EF Core translates to SQL.
/// </summary>
public class FilterEngineTests
{
    private static readonly WeaponCategory Weapons = new();

    private static Weapon W(string name, WeaponClass cls, int? rof, int? dice = 2, int? price = 100, bool conceal = false,
        int? magazine = 12, Caliber[]? calibers = null, string[]? tags = null) => new()
    {
        Name = name, WeaponClass = cls, Rof = rof, DamageDice = dice, PriceEb = price, Concealable = conceal, MagazineCapacity = magazine,
        Calibers = (calibers ?? []).Select(c => new WeaponCaliber { Caliber = c }).ToList(),
        Tags = (tags ?? []).Select(t => new ItemTag { Value = t }).ToList(),
    };

    private static readonly List<Weapon> Data =
    [
        W("SMG A", WeaponClass.Smg, 2, calibers: [Caliber.MediumPistol], tags: ["Smartgun", "Autofire"], conceal: true),
        W("SMG B", WeaponClass.Smg, 1, calibers: [Caliber.MediumPistol], tags: ["Autofire"]),
        W("Heavy SMG", WeaponClass.HeavySmg, 2, dice: 3, price: 500, calibers: [Caliber.HeavyPistol], tags: ["Smartgun"]),
        W("Pistol", WeaponClass.MediumPistol, 2, price: 50, calibers: [Caliber.MediumPistol], conceal: true),
        W("Shotgun", WeaponClass.Shotgun, 1, dice: 5, price: 500, magazine: 4, calibers: [Caliber.Slug, Caliber.ShotgunShell]),
        W("Exotic", WeaponClass.MediumPistol, null, dice: null, price: null, magazine: null),
    ];

    private static FilterCriterion C(string field, string op, object? value = null, object[]? values = null, object? min = null, object? max = null) => new()
    {
        Field = field,
        Op = op,
        Value = value is null ? null : JsonSerializer.SerializeToElement(value),
        Values = values?.Select(v => JsonSerializer.SerializeToElement(v)).ToList(),
        Min = min is null ? null : JsonSerializer.SerializeToElement(min),
        Max = max is null ? null : JsonSerializer.SerializeToElement(max),
    };

    private static List<string> Run(params FilterCriterion[] criteria) =>
        CategoryDefinition<Weapon>.ApplyCriteria(Data.AsQueryable(), Weapons.CompileCriteria(criteria), exceptField: null)
            .Select(w => w.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    [Fact]
    public void Type_and_rof_are_combined_with_AND()
    {
        Run(C("weaponClass", "in", values: ["Smg"]), C("rof", "eq", 2)).Should().Equal("SMG A");
    }

    [Fact]
    public void Values_inside_one_field_are_ORed()
    {
        Run(C("weaponClass", "in", values: ["Smg", "HeavySmg"])).Should().Equal("Heavy SMG", "SMG A", "SMG B");
    }

    [Fact]
    public void Enum_values_accept_member_names_and_ptBR_labels()
    {
        Run(C("weaponClass", "eq", "SMG Pesada")).Should().Equal("Heavy SMG");
        Run(C("weaponClass", "nin", values: ["Smg", "MediumPistol"])).Should().Equal("Heavy SMG", "Shotgun");
    }

    [Theory]
    [InlineData("gt", 2, new[] { "Heavy SMG", "Shotgun" })]
    [InlineData("gte", 3, new[] { "Heavy SMG", "Shotgun" })]
    [InlineData("lt", 3, new[] { "Pistol", "SMG A", "SMG B" })]
    [InlineData("lte", 2, new[] { "Pistol", "SMG A", "SMG B" })]
    [InlineData("eq", 5, new[] { "Shotgun" })]
    public void Numeric_comparisons_skip_items_without_value(string op, int value, string[] expected)
    {
        Run(C("damageDice", op, value)).Should().Equal(expected);
    }

    [Fact]
    public void Not_equal_keeps_items_without_value_like_SQL_with_EF_null_semantics()
    {
        Run(C("rof", "neq", 2)).Should().Equal("Exotic", "SMG B", "Shotgun");
    }

    [Fact]
    public void Between_supports_open_ranges()
    {
        Run(C("price", "between", min: 100, max: 500)).Should().Equal("Heavy SMG", "SMG A", "SMG B", "Shotgun");
        Run(C("price", "between", max: 50)).Should().Equal("Pistol");
        Run(C("price", "isnull")).Should().Equal("Exotic");
    }

    [Fact]
    public void Boolean_filter()
    {
        Run(C("concealable", "eq", true)).Should().Equal("Pistol", "SMG A");
    }

    [Fact]
    public void Set_fields_support_all_any_and_none()
    {
        Run(C("tags", "all", values: ["Smartgun", "Autofire"])).Should().Equal("SMG A");
        Run(C("tags", "in", values: ["Smartgun", "Autofire"])).Should().Equal("Heavy SMG", "SMG A", "SMG B");
        Run(C("calibers", "nin", values: ["MediumPistol"])).Should().Equal("Exotic", "Heavy SMG", "Shotgun");
        Run(C("calibers", "in", values: ["Slug"])).Should().Equal("Shotgun");
    }

    [Fact]
    public void Invalid_criteria_are_reported_together_per_field()
    {
        var act = () => Run(C("rof", "in", values: [1]), C("nope", "eq", 1), C("weaponClass", "in", values: ["Laser"]), C("rof", "gt"));
        var errors = act.Should().Throw<RequestValidationException>().Which.Errors;
        errors.Keys.Should().Contain(["rof", "nope", "weaponClass"]);
    }

    [Fact]
    public void Text_fields_are_not_filterable()
    {
        var act = () => Run(C("damage", "contains", "3d6"));
        act.Should().Throw<RequestValidationException>();
    }

    [Theory]
    [InlineData("asc", new[] { "Pistol", "SMG A", "SMG B", "Heavy SMG", "Shotgun", "Exotic" })]
    [InlineData("desc", new[] { "Heavy SMG", "Shotgun", "SMG A", "SMG B", "Pistol", "Exotic" })]
    public void Sorting_keeps_missing_values_last_and_breaks_ties_by_name(string dir, string[] expected)
    {
        Weapons.ApplySort(Data.AsQueryable(), "price", dir).Select(w => w.Name).Should().Equal(expected);
    }

    [Fact]
    public void Unknown_sort_is_rejected()
    {
        var act = () => Weapons.ApplySort(Data.AsQueryable(), "notes", "asc");
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Text_search_matches_every_token_ignoring_case_and_accents()
    {
        var items = new List<Weapon>
        {
            new() { Name = "a", SearchText = TextNormalizer.Normalize("Militech Escopeta Pesada") },
            new() { Name = "b", SearchText = TextNormalizer.Normalize("Arasaka Escopeta") },
        };
        CategoryDefinition<Weapon>.ApplyTextSearch(items.AsQueryable(), "ESCÓPETA militech").Select(w => w.Name).Should().Equal("a");
    }
}
