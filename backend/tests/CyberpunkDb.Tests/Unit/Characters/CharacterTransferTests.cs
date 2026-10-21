using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CharacterTransferTests
{
    /// <summary>The same catalog numbered differently, as in another database.</summary>
    private static readonly NpcCatalog Elsewhere = new(Catalog.Items.Select(i => i with { Id = i.Id + 1000 }));

    private static CharacterSheet Chromed() => CompletePackage() with
    {
        Inventory =
        [
            new InventoryEntry { InstanceId = "it-jack", ItemId = LightArmorjack.Id },
        ],
        Cyberware =
        [
            new CharacterCyberware { InstanceId = "cw-link", ItemId = NeuralLink.Id },
            new CharacterCyberware { InstanceId = "cw-sande", ItemId = Sandevistan.Id, ParentInstanceId = "cw-link" },
        ],
        Style = new StyleState { OutfitItems = ["it-jack"] },
    };

    [Fact]
    public void Items_are_found_again_by_slug_in_a_catalog_numbered_differently()
    {
        var sheet = Chromed();
        var items = CharacterTransfer.ReferencedItems(sheet, Catalog);
        items.Values.Select(i => i.Slug).Should().BeEquivalentTo(["light-armorjack", "neural-link", "sandevistan"]);

        var (rebound, missing) = CharacterTransfer.Rebind(sheet, items, Elsewhere);
        missing.Should().BeEmpty();
        rebound.Inventory.Single().ItemId.Should().Be(LightArmorjack.Id + 1000);
        rebound.Cyberware.Select(c => c.ItemId).Should().Equal(NeuralLink.Id + 1000, Sandevistan.Id + 1000);
        rebound.Style.OutfitItems.Should().Equal("it-jack");
    }

    [Fact]
    public void Items_missing_from_the_catalog_are_left_out_never_matched_to_another()
    {
        var sheet = Chromed();
        var lacking = new NpcCatalog(Elsewhere.Items.Where(i => i.Slug is not ("neural-link" or "light-armorjack")));

        var (rebound, missing) = CharacterTransfer.Rebind(sheet, CharacterTransfer.ReferencedItems(sheet, Catalog), lacking);

        rebound.Inventory.Should().BeEmpty();
        rebound.Cyberware.Should().BeEmpty("o Sandevistan estava instalado no Neural Link que ficou de fora");
        rebound.Style.OutfitItems.Should().BeEmpty();
        missing.Should().HaveCount(3);
    }

    [Fact]
    public void File_names_drop_accents_and_symbols()
    {
        CharacterTransfer.FileName(new CharacterIdentity { Name = "Suzu", Handle = "Neon" }).Should().Be("suzu-neon.cyberpunk.json");
        CharacterTransfer.FileName(new CharacterIdentity { Name = "João Ação", Handle = "\"Razor\"" }).Should().Be("joao-acao-razor.cyberpunk.json");
        CharacterTransfer.FileName(new CharacterIdentity { Name = "???" }).Should().Be("personagem.cyberpunk.json");
    }
}
