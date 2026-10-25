using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Encounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    StateJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_UpdatedAt",
                table: "Encounters",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Encounters");
        }
    }
}
