using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SameieHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardCaseDecisionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DecisionDate",
                table: "BoardCases",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionDate",
                table: "BoardCases");
        }
    }
}
