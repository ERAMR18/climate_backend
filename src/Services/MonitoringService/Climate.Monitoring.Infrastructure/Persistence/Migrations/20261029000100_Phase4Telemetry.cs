using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Climate.Monitoring.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MonitoringDbContext))]
[Migration("20261029000100_Phase4Telemetry")]
public sealed class Phase4Telemetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("TelemetryDispatched", "SensorReadings", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("Source", "SensorReadings", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "SIMULATED");
        migrationBuilder.AddColumn<string>("DeviceId", "SensorReadings", type: "nvarchar(100)", maxLength: 100, nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("TelemetryDispatched", "SensorReadings");
        migrationBuilder.DropColumn("DeviceId", "SensorReadings");
        migrationBuilder.DropColumn("Source", "SensorReadings");
    }
}
