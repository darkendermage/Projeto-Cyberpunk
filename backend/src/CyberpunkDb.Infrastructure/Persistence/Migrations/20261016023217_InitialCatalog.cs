using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyberpunkDb.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "AppMetadata",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMetadata", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "GearLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Manufacturers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RuleSystem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PriceEb = table.Column<int>(type: "integer", nullable: true),
                    PriceNote = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PriceCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    SourceBookId = table.Column<int>(type: "integer", nullable: true),
                    Page = table.Column<int>(type: "integer", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RuleSystem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DataOrigin = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExtraAttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_SourceBooks_SourceBookId",
                        column: x => x.SourceBookId,
                        principalTable: "SourceBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Ammunition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EffectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuantityPerPack = table.Column<int>(type: "integer", nullable: true),
                    Damage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DamageDice = table.Column<int>(type: "integer", nullable: true),
                    AreaOfEffect = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResistDv = table.Column<int>(type: "integer", nullable: true),
                    Requirements = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ammunition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ammunition_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArmorItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ArmorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StoppingPower = table.Column<int>(type: "integer", nullable: true),
                    ShieldHp = table.Column<int>(type: "integer", nullable: true),
                    RefPenalty = table.Column<int>(type: "integer", nullable: true),
                    DexPenalty = table.Column<int>(type: "integer", nullable: true),
                    MovePenalty = table.Column<int>(type: "integer", nullable: true),
                    BodyValue = table.Column<int>(type: "integer", nullable: true),
                    HitPoints = table.Column<int>(type: "integer", nullable: true),
                    HumanityLoss = table.Column<int>(type: "integer", nullable: true),
                    HumanityLossDice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InstallLocation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ClothingSlot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ClothingStyle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProtectedLocations = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmorItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArmorItems_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CyberwareItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CyberwareType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BodyLocation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsFoundational = table.Column<bool>(type: "boolean", nullable: false),
                    SlotsProvided = table.Column<int>(type: "integer", nullable: true),
                    SlotsRequired = table.Column<int>(type: "integer", nullable: true),
                    HumanityLoss = table.Column<int>(type: "integer", nullable: true),
                    HumanityLossDice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InstallLocation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequiresPair = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresItemId = table.Column<int>(type: "integer", nullable: true),
                    Requirements = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CyberwareItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CyberwareItems_CyberwareItems_RequiresItemId",
                        column: x => x.RequiresItemId,
                        principalTable: "CyberwareItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CyberwareItems_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResistDv = table.Column<int>(type: "integer", nullable: true),
                    PrimaryEffect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Duration = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    SideEffects = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddictionRisk = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    HumanityLoss = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drugs_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FashionwareItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    FashionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BodyArea = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VisualEffect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FunctionalEffect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InstallLocation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HumanityLoss = table.Column<int>(type: "integer", nullable: true),
                    HumanityLossDice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FashionwareItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FashionwareItems_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_Favorites_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GearItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    GearType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Subtype = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SkillBonus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GearItems_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GearListEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GearListId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GearListEntries_GearLists_GearListId",
                        column: x => x.GearListId,
                        principalTable: "GearLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GearListEntries_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HqUpgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UpgradeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Benefits = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImprovementPoints = table.Column<int>(type: "integer", nullable: true),
                    SpaceRequired = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HqUpgrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HqUpgrades_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTags",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTags", x => new { x.ItemId, x.Value });
                    table.ForeignKey(
                        name: "FK_ItemTags_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SpeedMove = table.Column<int>(type: "integer", nullable: true),
                    TopSpeedMph = table.Column<int>(type: "integer", nullable: true),
                    Maneuverability = table.Column<int>(type: "integer", nullable: true),
                    Seats = table.Column<int>(type: "integer", nullable: true),
                    CargoKg = table.Column<int>(type: "integer", nullable: true),
                    StructuralDamagePoints = table.Column<int>(type: "integer", nullable: true),
                    ArmorSp = table.Column<int>(type: "integer", nullable: true),
                    IncludedUpgrades = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Weapons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    WeaponClass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Skill = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Damage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DamageDice = table.Column<int>(type: "integer", nullable: true),
                    Rof = table.Column<int>(type: "integer", nullable: true),
                    AutofireMultiplier = table.Column<int>(type: "integer", nullable: true),
                    Hands = table.Column<int>(type: "integer", nullable: false),
                    Concealable = table.Column<bool>(type: "boolean", nullable: false),
                    MagazineCapacity = table.Column<int>(type: "integer", nullable: true),
                    Quality = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsExotic = table.Column<bool>(type: "boolean", nullable: false),
                    MaxRangeMeters = table.Column<int>(type: "integer", nullable: true),
                    MinBody = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Weapons_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AmmunitionCalibers",
                columns: table => new
                {
                    AmmunitionId = table.Column<int>(type: "integer", nullable: false),
                    Caliber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmmunitionCalibers", x => new { x.AmmunitionId, x.Caliber });
                    table.ForeignKey(
                        name: "FK_AmmunitionCalibers_Ammunition_AmmunitionId",
                        column: x => x.AmmunitionId,
                        principalTable: "Ammunition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeaponCalibers",
                columns: table => new
                {
                    WeaponId = table.Column<int>(type: "integer", nullable: false),
                    Caliber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeaponCalibers", x => new { x.WeaponId, x.Caliber });
                    table.ForeignKey(
                        name: "FK_WeaponCalibers_Weapons_WeaponId",
                        column: x => x.WeaponId,
                        principalTable: "Weapons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ammunition_Kind",
                table: "Ammunition",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_AmmunitionCalibers_Caliber",
                table: "AmmunitionCalibers",
                column: "Caliber");

            migrationBuilder.CreateIndex(
                name: "IX_ArmorItems_ArmorType",
                table: "ArmorItems",
                column: "ArmorType");

            migrationBuilder.CreateIndex(
                name: "IX_ArmorItems_StoppingPower",
                table: "ArmorItems",
                column: "StoppingPower");

            migrationBuilder.CreateIndex(
                name: "IX_CyberwareItems_CyberwareType",
                table: "CyberwareItems",
                column: "CyberwareType");

            migrationBuilder.CreateIndex(
                name: "IX_CyberwareItems_HumanityLoss",
                table: "CyberwareItems",
                column: "HumanityLoss");

            migrationBuilder.CreateIndex(
                name: "IX_CyberwareItems_RequiresItemId",
                table: "CyberwareItems",
                column: "RequiresItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Classification",
                table: "Drugs",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_FashionwareItems_FashionType",
                table: "FashionwareItems",
                column: "FashionType");

            migrationBuilder.CreateIndex(
                name: "IX_GearItems_GearType",
                table: "GearItems",
                column: "GearType");

            migrationBuilder.CreateIndex(
                name: "IX_GearListEntries_GearListId_ItemId",
                table: "GearListEntries",
                columns: new[] { "GearListId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearListEntries_ItemId",
                table: "GearListEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Category",
                table: "Items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ManufacturerId",
                table: "Items",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                table: "Items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PriceEb",
                table: "Items",
                column: "PriceEb");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SearchText",
                table: "Items",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Slug",
                table: "Items",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_SourceBookId",
                table: "Items",
                column: "SourceBookId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTags_Value",
                table: "ItemTags",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_Slug",
                table: "Manufacturers",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceBooks_Slug",
                table: "SourceBooks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleType",
                table: "Vehicles",
                column: "VehicleType");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponCalibers_Caliber",
                table: "WeaponCalibers",
                column: "Caliber");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_DamageDice",
                table: "Weapons",
                column: "DamageDice");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_Rof",
                table: "Weapons",
                column: "Rof");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_WeaponClass",
                table: "Weapons",
                column: "WeaponClass");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmmunitionCalibers");

            migrationBuilder.DropTable(
                name: "AppMetadata");

            migrationBuilder.DropTable(
                name: "ArmorItems");

            migrationBuilder.DropTable(
                name: "CyberwareItems");

            migrationBuilder.DropTable(
                name: "Drugs");

            migrationBuilder.DropTable(
                name: "FashionwareItems");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "GearItems");

            migrationBuilder.DropTable(
                name: "GearListEntries");

            migrationBuilder.DropTable(
                name: "HqUpgrades");

            migrationBuilder.DropTable(
                name: "ItemTags");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "WeaponCalibers");

            migrationBuilder.DropTable(
                name: "Ammunition");

            migrationBuilder.DropTable(
                name: "GearLists");

            migrationBuilder.DropTable(
                name: "Weapons");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Manufacturers");

            migrationBuilder.DropTable(
                name: "SourceBooks");
        }
    }
}
