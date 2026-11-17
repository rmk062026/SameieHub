using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SameieHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingUnitDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AreaSquareMeters",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HousingType",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "HousingUnits",
                type: "INTEGER",
                nullable: false,
                // Units registered before this migration must stay active.
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhoneNumber",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondOwnerEmail",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondOwnerName",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondOwnerPhoneNumber",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionNumber",
                table: "HousingUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNumber",
                table: "HousingUnits",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaSquareMeters",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "HousingType",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "OwnerPhoneNumber",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "SecondOwnerEmail",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "SecondOwnerName",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "SecondOwnerPhoneNumber",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "SectionNumber",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "HousingUnits");
        }
    }
}
