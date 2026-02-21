using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace shared.Migrations
{
    /// <inheritdoc />
    public partial class migration4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DigitalTwinTelemetry",
                table: "DigitalTwinTelemetry");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DigitalTwinTelemetry",
                table: "DigitalTwinTelemetry",
                columns: new[] { "Id", "RunId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DigitalTwinTelemetry",
                table: "DigitalTwinTelemetry");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DigitalTwinTelemetry",
                table: "DigitalTwinTelemetry",
                columns: new[] { "RunId", "Timestamp" });
        }
    }
}
