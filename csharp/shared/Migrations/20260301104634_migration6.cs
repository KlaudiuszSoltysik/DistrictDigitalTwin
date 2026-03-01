using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace shared.Migrations
{
    /// <inheritdoc />
    public partial class migration6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoomHeatings",
                table: "SimulationTelemetry",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoomHeatings",
                table: "DigitalTwinTelemetry",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoomHeatings",
                table: "SimulationTelemetry");

            migrationBuilder.DropColumn(
                name: "RoomHeatings",
                table: "DigitalTwinTelemetry");
        }
    }
}
