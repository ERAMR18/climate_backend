using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Monitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMonitoring : Migration
    {
        private static readonly string[] CommunityTimeColumns = ["CommunityId", "RecordedAt"];
        private static readonly string[] SensorTimeColumns = ["SensorId", "RecordedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SensorReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_CommunityId_RecordedAt",
                table: "SensorReadings",
                columns: CommunityTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_RecordedAt",
                table: "SensorReadings",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_SensorId_RecordedAt",
                table: "SensorReadings",
                columns: SensorTimeColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensorReadings");
        }
    }
}
