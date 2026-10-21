using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Infrastructure.Persistence;

/// <summary>
/// Table-per-type mapping: "Items" holds the shared columns and every category has its own table
/// with typed columns, joined by primary key. Indexes cover the most common filters and sorts.
/// </summary>
internal static class CatalogModelConfiguration
{
    public static void Apply(ModelBuilder b, bool isPostgres)
    {
        if (isPostgres) b.HasPostgresExtension("pg_trgm");

        b.Entity<Manufacturer>(e =>
        {
            e.ToTable("Manufacturers");
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<SourceBook>(e =>
        {
            e.ToTable("SourceBooks");
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Item>(e =>
        {
            e.ToTable("Items");
            e.UseTptMappingStrategy();
            e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Property(x => x.PriceNote).HasMaxLength(120);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.SearchText).IsRequired();
            if (isPostgres) e.Property(x => x.ExtraAttributesJson).HasColumnType("jsonb");

            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.PriceEb);
            e.HasIndex(x => x.ManufacturerId);
            if (isPostgres) e.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");

            e.HasOne(x => x.Manufacturer).WithMany().HasForeignKey(x => x.ManufacturerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SourceBook).WithMany().HasForeignKey(x => x.SourceBookId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Tags).WithOne().HasForeignKey(t => t.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ItemTag>(e =>
        {
            e.ToTable("ItemTags");
            e.HasKey(x => new { x.ItemId, x.Value });
            e.Property(x => x.Value).HasMaxLength(40);
            e.HasIndex(x => x.Value);
        });

        b.Entity<Weapon>(e =>
        {
            e.ToTable("Weapons");
            e.Property(x => x.Damage).HasMaxLength(40);
            e.HasIndex(x => x.WeaponClass);
            e.HasIndex(x => x.Rof);
            e.HasIndex(x => x.DamageDice);
            e.HasMany(x => x.Calibers).WithOne().HasForeignKey(c => c.WeaponId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<WeaponCaliber>(e =>
        {
            e.ToTable("WeaponCalibers");
            e.HasKey(x => new { x.WeaponId, x.Caliber });
            e.HasIndex(x => x.Caliber);
        });

        b.Entity<Ammunition>(e =>
        {
            e.ToTable("Ammunition");
            e.Property(x => x.Damage).HasMaxLength(40);
            e.Property(x => x.AreaOfEffect).HasMaxLength(80);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.HasIndex(x => x.Kind);
            e.HasMany(x => x.Calibers).WithOne().HasForeignKey(c => c.AmmunitionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AmmunitionCaliber>(e =>
        {
            e.ToTable("AmmunitionCalibers");
            e.HasKey(x => new { x.AmmunitionId, x.Caliber });
            e.HasIndex(x => x.Caliber);
        });

        b.Entity<GearItem>(e =>
        {
            e.ToTable("GearItems");
            e.Property(x => x.Subtype).HasMaxLength(60);
            e.Property(x => x.WeightKg).HasPrecision(10, 2);
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.Property(x => x.SkillBonus).HasMaxLength(120);
            e.HasIndex(x => x.GearType);
        });

        b.Entity<ArmorItem>(e =>
        {
            e.ToTable("ArmorItems");
            e.Property(x => x.HumanityLossDice).HasMaxLength(20);
            e.Property(x => x.ProtectedLocations).HasMaxLength(120);
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.HasIndex(x => x.ArmorType);
            e.HasIndex(x => x.StoppingPower);
        });

        b.Entity<CyberwareItem>(e =>
        {
            e.ToTable("CyberwareItems");
            e.Property(x => x.HumanityLossDice).HasMaxLength(20);
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.HasIndex(x => x.CyberwareType);
            e.HasIndex(x => x.HumanityLoss);
            // Self reference: an option (e.g. Low Light/IR/UV) requires a foundational piece (Cybereye).
            e.HasOne(x => x.RequiresItem).WithMany().HasForeignKey(x => x.RequiresItemId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WeaponAttachment>(e =>
        {
            e.ToTable("WeaponAttachments");
            e.Property(x => x.EligibleWeapons).HasMaxLength(300);
            e.Property(x => x.Installation).HasMaxLength(200);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.HasIndex(x => x.AttachmentType);
            e.HasMany(x => x.CompatibleClasses).WithOne().HasForeignKey(c => c.WeaponAttachmentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WeaponAttachmentClass>(e =>
        {
            e.ToTable("WeaponAttachmentClasses");
            e.HasKey(x => new { x.WeaponAttachmentId, x.WeaponClass });
            e.HasIndex(x => x.WeaponClass);
        });

        b.Entity<NetrunningItem>(e =>
        {
            e.ToTable("NetrunningItems");
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.HasIndex(x => x.NetType);
            e.HasIndex(x => x.ProgramClass);
        });

        b.Entity<Drug>(e =>
        {
            e.ToTable("Drugs");
            e.Property(x => x.PrimaryEffect).HasMaxLength(1000);
            e.Property(x => x.Duration).HasMaxLength(80);
            e.Property(x => x.SideEffects).HasMaxLength(1000);
            e.HasIndex(x => x.Classification);
        });

        b.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles");
            e.Property(x => x.IncludedUpgrades).HasMaxLength(1000);
            e.HasIndex(x => x.VehicleType);
            e.HasIndex(x => x.MotoRank);
        });

        b.Entity<VehicleUpgrade>(e =>
        {
            e.ToTable("VehicleUpgrades");
            e.Property(x => x.EligibleVehicles).HasMaxLength(300);
            e.Property(x => x.Effect).HasMaxLength(1000);
            e.HasIndex(x => x.UpgradeType);
        });

        b.Entity<HqUpgrade>(e =>
        {
            e.ToTable("HqUpgrades");
            e.Property(x => x.Benefits).HasMaxLength(1000);
            e.Property(x => x.Requirements).HasMaxLength(300);
            e.Property(x => x.SpaceRequired).HasMaxLength(120);
        });

        b.Entity<Favorite>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(x => x.ItemId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GearList>(e =>
        {
            e.ToTable("GearLists");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.GearListId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AppMetadata>(e =>
        {
            e.ToTable("AppMetadata");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(60);
            e.Property(x => x.Value).HasMaxLength(500);
        });

        b.Entity<Npc>(e =>
        {
            e.ToTable("Npcs");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Concept).HasMaxLength(300);
            e.Property(x => x.ProfileId).HasMaxLength(60);
            e.Property(x => x.SheetJson).IsRequired();
            if (isPostgres) e.Property(x => x.SheetJson).HasColumnType("jsonb");
            e.HasIndex(x => x.UpdatedAt);
        });

        b.Entity<Character>(e =>
        {
            e.ToTable("Characters");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Handle).HasMaxLength(80);
            e.Property(x => x.Role).HasMaxLength(40);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.SheetJson).IsRequired();
            if (isPostgres) e.Property(x => x.SheetJson).HasColumnType("jsonb");
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Portrait).WithOne().HasForeignKey<CharacterPortrait>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UpdatedAt);
        });

        b.Entity<CharacterPortrait>(e =>
        {
            e.ToTable("CharacterPortraits");
            e.HasKey(x => x.CharacterId);
            e.Property(x => x.ContentType).HasMaxLength(60).IsRequired();
            e.Property(x => x.Data).IsRequired();
        });

        b.Entity<CharacterEvent>(e =>
        {
            e.ToTable("CharacterEvents");
            e.Property(x => x.Action).HasMaxLength(60).IsRequired();
            e.Property(x => x.Before).HasMaxLength(200);
            e.Property(x => x.After).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            if (isPostgres) e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.CharacterId, x.Kind, x.CreatedAt });
        });

        b.Entity<GearListEntry>(e =>
        {
            e.ToTable("GearListEntries");
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.GearListId, x.ItemId }).IsUnique();
        });
    }
}
