using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace shared.Migrations
{
    /// <inheritdoc />
    public partial class m1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalTwinTelemetry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    WindSpeed = table.Column<double>(type: "double precision", nullable: false),
                    WindDirection = table.Column<double>(type: "double precision", nullable: false),
                    SunRadiation = table.Column<double>(type: "double precision", nullable: false),
                    SunAltitude = table.Column<double>(type: "double precision", nullable: false),
                    SunAzimuth = table.Column<double>(type: "double precision", nullable: false),
                    RoomTemperatures = table.Column<string>(type: "jsonb", nullable: false),
                    RoomHeatings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalTwinTelemetry", x => new { x.Id, x.RunId, x.Timestamp });
                });

            migrationBuilder.CreateTable(
                name: "SimulationTelemetry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    WindSpeed = table.Column<double>(type: "double precision", nullable: false),
                    WindDirection = table.Column<double>(type: "double precision", nullable: false),
                    SunRadiation = table.Column<double>(type: "double precision", nullable: false),
                    SunAltitude = table.Column<double>(type: "double precision", nullable: false),
                    SunAzimuth = table.Column<double>(type: "double precision", nullable: false),
                    RoomTemperatures = table.Column<string>(type: "jsonb", nullable: false),
                    RoomHeatings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationTelemetry", x => new { x.Id, x.RunId, x.Timestamp });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalTwinTelemetry_RunId",
                table: "DigitalTwinTelemetry",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationTelemetry_RunId",
                table: "SimulationTelemetry",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalTwinTelemetry");

            migrationBuilder.DropTable(
                name: "SimulationTelemetry");
        }
    }
}
