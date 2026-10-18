using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NetrunningAndMoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "MotoRank",
                table: "Vehicles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NetrunningItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NetType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ProgramClass = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Perception = table.Column<int>(type: "INTEGER", nullable: true),
                    Speed = table.Column<int>(type: "INTEGER", nullable: true),
                    Attack = table.Column<int>(type: "INTEGER", nullable: true),
                    Defense = table.Column<int>(type: "INTEGER", nullable: true),
                    Rez = table.Column<int>(type: "INTEGER", nullable: true),
                    SlotsRequired = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgramSlots = table.Column<int>(type: "INTEGER", nullable: true),
                    HardwareSlots = table.Column<int>(type: "INTEGER", nullable: true),
                    SharedSlots = table.Column<int>(type: "INTEGER", nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Effect = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetrunningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetrunningItems_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeaponAttachmentClasses",
                columns: table => new
                {
                    WeaponAttachmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeaponClass = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeaponAttachmentClasses", x => new { x.WeaponAttachmentId, x.WeaponClass });
                    table.ForeignKey(
                        name: "FK_WeaponAttachmentClasses_WeaponAttachments_WeaponAttachmentId",
                        column: x => x.WeaponAttachmentId,
                        principalTable: "WeaponAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_MotoRank",
                table: "Vehicles",
                column: "MotoRank");

            migrationBuilder.CreateIndex(
                name: "IX_NetrunningItems_NetType",
                table: "NetrunningItems",
                column: "NetType");

            migrationBuilder.CreateIndex(
                name: "IX_NetrunningItems_ProgramClass",
                table: "NetrunningItems",
                column: "ProgramClass");

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAttachmentClasses_WeaponClass",
                table: "WeaponAttachmentClasses",
                column: "WeaponClass");

            // Fashionware is a cyberware type (CP:R p.111): rows move to CyberwareItems keeping their Id,
            // so favorites and list entries keep pointing at them. The SQL is valid on PostgreSQL and SQLite.
            migrationBuilder.Sql("""
                INSERT INTO "CyberwareItems" ("Id", "CyberwareType", "BodyLocation", "IsFoundational", "SlotsProvided", "SlotsRequired",
                    "HumanityLoss", "HumanityLossDice", "InstallLocation", "RequiresPair", "RequiresItemId", "Requirements", "Effect")
                SELECT "Id", 'Fashionware', "BodyArea", FALSE, NULL, NULL, "HumanityLoss", "HumanityLossDice", "InstallLocation", FALSE, NULL, NULL,
                    NULLIF(SUBSTR(TRIM(COALESCE("VisualEffect", '') || ' ' || COALESCE("FunctionalEffect", '')), 1, 1000), '')
                FROM "FashionwareItems";
                """);
            migrationBuilder.Sql("""UPDATE "Items" SET "Category" = 'Cyberware' WHERE "Category" = 'Fashionware';""");

            // Cyberdecks, Programs, Black ICE and Cyberdeck Hardware leave Gear for the new Netrunning category.
            // Stats (ATK/DEF/REZ, slots) come with the next seed import, which upserts by slug.
            migrationBuilder.Sql("""
                INSERT INTO "NetrunningItems" ("Id", "NetType", "ProgramClass", "Requirements", "Effect")
                SELECT "Id",
                    CASE WHEN "GearType" = 'Program' THEN 'Program'
                         WHEN "GearType" = 'BlackIce' THEN 'BlackIce'
                         WHEN "Subtype" = 'Cyberdeck' THEN 'Cyberdeck'
                         ELSE 'Hardware' END,
                    CASE "Subtype" WHEN 'Booster' THEN 'Booster' WHEN 'Defender' THEN 'Defender'
                         WHEN 'Anti-Pessoal' THEN 'AntiPersonnel' WHEN 'Anti-Programa' THEN 'AntiProgram' ELSE NULL END,
                    "Requirements", "Effect"
                FROM "GearItems"
                WHERE "GearType" IN ('Program', 'BlackIce') OR "Subtype" IN ('Cyberdeck', 'Hardware de Cyberdeck');
                """);
            migrationBuilder.Sql("""UPDATE "Items" SET "Category" = 'Netrunning' WHERE "Id" IN (SELECT "Id" FROM "NetrunningItems");""");
            migrationBuilder.Sql("""DELETE FROM "GearItems" WHERE "Id" IN (SELECT "Id" FROM "NetrunningItems");""");

            migrationBuilder.DropTable(
                name: "FashionwareItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetrunningItems");

            migrationBuilder.DropTable(
                name: "WeaponAttachmentClasses");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_MotoRank",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "MotoRank",
                table: "Vehicles");

            migrationBuilder.CreateTable(
                name: "FashionwareItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BodyArea = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FashionType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FunctionalEffect = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    HumanityLoss = table.Column<int>(type: "INTEGER", nullable: true),
                    HumanityLossDice = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InstallLocation = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    VisualEffect = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_FashionwareItems_FashionType",
                table: "FashionwareItems",
                column: "FashionType");
        }
    }
}
