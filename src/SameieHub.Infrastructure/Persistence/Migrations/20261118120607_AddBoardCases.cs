using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SameieHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", nullable: true),
                    HousingUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCases_HousingUnits_HousingUnitId",
                        column: x => x.HousingUnitId,
                        principalTable: "HousingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardCases_HousingUnitId",
                table: "BoardCases",
                column: "HousingUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardCases");
        }
    }
}
