using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NetArchitectures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetArchitectures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Difficulty = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Floors = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    StateJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetArchitectures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetArchitectures_UpdatedAt",
                table: "NetArchitectures",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetArchitectures");
        }
    }
}
