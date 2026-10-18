using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VehicleUpgrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleUpgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UpgradeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MotoRank = table.Column<int>(type: "integer", nullable: true),
                    EligibleVehicles = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleUpgrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleUpgrades_Items_Id",
                        column: x => x.Id,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleUpgrades_UpgradeType",
                table: "VehicleUpgrades",
                column: "UpgradeType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleUpgrades");
        }
    }
}
