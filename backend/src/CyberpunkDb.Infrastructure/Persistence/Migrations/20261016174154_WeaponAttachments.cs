using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AttachmentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SlotsRequired = table.Column<int>(type: "integer", nullable: true),
                    ExoticAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    EligibleWeapons = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Installation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
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
