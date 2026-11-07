using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberpunkDb.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class TableSessionsAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiceColor",
                table: "TableSeats",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "TableMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "Encounters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TableSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TableSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSettings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableMessages_SessionId",
                table: "TableMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_SessionId",
                table: "Encounters",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TableSessions_EndedAt",
                table: "TableSessions",
                column: "EndedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_TableMessages_TableSessions_SessionId",
                table: "TableMessages",
                column: "SessionId",
                principalTable: "TableSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TableMessages_TableSessions_SessionId",
                table: "TableMessages");

            migrationBuilder.DropTable(
                name: "TableSessions");

            migrationBuilder.DropTable(
                name: "TableSettings");

            migrationBuilder.DropIndex(
                name: "IX_TableMessages_SessionId",
                table: "TableMessages");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_SessionId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "DiceColor",
                table: "TableSeats");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "TableMessages");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Encounters");
        }
    }
}
