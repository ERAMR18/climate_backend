using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Monitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2RulesWorkflowAndReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SensorWasActive",
                table: "SensorReadings",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SimulationOverrides",
                columns: table => new
                {
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationOverrides", x => x.SensorId);
                });
            migrationBuilder.Sql("UPDATE SensorReadings SET RecordedAt = SWITCHOFFSET(RecordedAt, '+00:00');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulationOverrides");

            migrationBuilder.DropColumn(
                name: "SensorWasActive",
                table: "SensorReadings");
        }
    }
}
