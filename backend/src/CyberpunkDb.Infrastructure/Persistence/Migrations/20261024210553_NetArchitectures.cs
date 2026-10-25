using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyberpunkDb.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Floors = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
