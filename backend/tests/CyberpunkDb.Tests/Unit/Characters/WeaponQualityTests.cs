using System.Text.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

/// <summary>Weapon quality per copy (A4): Poor, Standard or Excellent, defaulting to the catalog's.</summary>
public class WeaponQualityTests
{
    private static CharacterSheet WithWeapons(params (string Id, CatalogItem Item, WeaponQuality? Quality)[] weapons) =>
        CompletePackage() with
        {
            Inventory = weapons.Select(w => new InventoryEntry { InstanceId = w.Id, ItemId = w.Item.Id, State = ItemState.Equipped, Quality = w.Quality }).ToList(),
        };

    [Fact]
    public void Quality_is_saved_per_instance()
    {
        var sheet = WithWeapons(("a", HeavyPistol, null), ("b", HeavyPistol, null));
        var change = new CharacterChange(sheet, Catalog, DateTime.UtcNow);

        var after = InventoryOperations.SetQuality(sheet, new WeaponQualityRequest(0, "b", WeaponQuality.Excellent), change);

        var lines = Calculator.Calculate(after.Normalize(), Catalog).Weapons;
        lines.Single(w => w.InstanceId == "a").Quality.Should().Be(WeaponQuality.Standard);
        lines.Single(w => w.InstanceId == "b").Quality.Should().Be(WeaponQuality.Excellent);
    }

    [Fact]
    public void Default_quality_from_catalog()
    {
        var lines = Calculator.Calculate(WithWeapons(("p", PoorPistol, null), ("s", HeavyPistol, null)).Normalize(), Catalog).Weapons;

        lines.Single(w => w.InstanceId == "p").Quality.Should().Be(WeaponQuality.Poor);
        lines.Single(w => w.InstanceId == "s").Quality.Should().Be(WeaponQuality.Standard);
    }

    [Fact]
    public void Quality_only_on_weapons()
    {
        var sheet = CompletePackage() with { Inventory = [new InventoryEntry { InstanceId = "x", ItemId = LightArmorjack.Id }] };
        var act = () => InventoryOperations.SetQuality(sheet, new WeaponQualityRequest(0, "x", WeaponQuality.Excellent), new CharacterChange(sheet, Catalog, DateTime.UtcNow));

        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Old_sheet_json_loads()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonNode.Parse(JsonSerializer.Serialize(WithWeapons(("s", HeavyPistol, null)), web))!;
        foreach (var e in node["inventory"]!.AsArray()) e!.AsObject().Remove("quality");
        var sheet = node.Deserialize<CharacterSheet>(web)!.Normalize();

        Calculator.Calculate(sheet, Catalog).Weapons.Single().Quality.Should().Be(WeaponQuality.Standard);
    }
}
