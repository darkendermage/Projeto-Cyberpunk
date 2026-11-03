using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NpcPortraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompendiumPortraits",
                columns: table => new
                {
                    CompendiumId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompendiumPortraits", x => x.CompendiumId);
                });

            migrationBuilder.CreateTable(
                name: "NpcPortraits",
                columns: table => new
                {
                    NpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcPortraits", x => x.NpcId);
                    table.ForeignKey(
                        name: "FK_NpcPortraits_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompendiumPortraits");

            migrationBuilder.DropTable(
                name: "NpcPortraits");
        }
    }
}
