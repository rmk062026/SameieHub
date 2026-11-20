using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SameieHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingFacility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargingFacilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingFacilities", x => x.Id);
                    table.CheckConstraint("CK_ChargingFacilities_OneFacility", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "EvChargers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargingFacilityId = table.Column<int>(type: "INTEGER", nullable: false),
                    HousingUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargerNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    HasElectricVehicle = table.Column<bool>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvChargers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvChargers_ChargingFacilities_ChargingFacilityId",
                        column: x => x.ChargingFacilityId,
                        principalTable: "ChargingFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvChargers_HousingUnits_HousingUnitId",
                        column: x => x.HousingUnitId,
                        principalTable: "HousingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CabinetNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    ChargingFacilityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    EvChargerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.CheckConstraint("CK_Meters_KindFields", "(\"Kind\" = 'Main' AND \"EvChargerId\" IS NULL AND \"CabinetNumber\" IS NULL) OR (\"Kind\" = 'Sub' AND \"EvChargerId\" IS NOT NULL AND \"CabinetNumber\" IS NOT NULL AND \"CabinetNumber\" IN (1, 2))");
                    table.ForeignKey(
                        name: "FK_Meters_ChargingFacilities_ChargingFacilityId",
                        column: x => x.ChargingFacilityId,
                        principalTable: "ChargingFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meters_EvChargers_EvChargerId",
                        column: x => x.EvChargerId,
                        principalTable: "EvChargers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kwh = table.Column<decimal>(type: "TEXT", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.CheckConstraint("CK_MeterReadings_NonNegativeKwh", "CAST(\"Kwh\" AS REAL) >= 0");
                    table.ForeignKey(
                        name: "FK_MeterReadings_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvChargers_ChargingFacilityId",
                table: "EvChargers",
                column: "ChargingFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_EvChargers_HousingUnitId",
                table: "EvChargers",
                column: "HousingUnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_MeterId_ReadAt",
                table: "MeterReadings",
                columns: new[] { "MeterId", "ReadAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meters_ChargingFacilityId",
                table: "Meters",
                column: "ChargingFacilityId",
                unique: true,
                filter: "\"Kind\" = 'Main'");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_EvChargerId",
                table: "Meters",
                column: "EvChargerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "Meters");

            migrationBuilder.DropTable(
                name: "EvChargers");

            migrationBuilder.DropTable(
                name: "ChargingFacilities");
        }
    }
}
