using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Alerts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAlerts : Migration
    {
        private static readonly string[] CommunityStatusColumns = ["CommunityId", "IsActive"];
        private static readonly string[] LevelStatusColumns = ["Level", "IsActive"];
        private static readonly string[] SensorRiskStatusColumns = ["SensorId", "AlertType", "IsActive"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SensorValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CommunityId_IsActive",
                table: "Alerts",
                columns: CommunityStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_GeneratedAt",
                table: "Alerts",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Level_IsActive",
                table: "Alerts",
                columns: LevelStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_SensorId_AlertType_IsActive",
                table: "Alerts",
                columns: SensorRiskStatusColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");
        }
    }
}
