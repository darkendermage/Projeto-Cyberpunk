using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class WeaponAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeaponAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttachmentType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SlotsRequired = table.Column<int>(type: "INTEGER", nullable: true),
                    ExoticAllowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    EligibleWeapons = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Installation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Effect = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeaponAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeaponAttachments_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeaponAttachments_AttachmentType",
                table: "WeaponAttachments",
                column: "AttachmentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeaponAttachments");
        }
    }
}
