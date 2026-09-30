using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Alerts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2RulesWorkflowAndReadings : Migration
    {
        private static readonly string[] AlertFilterColumns = ["CommunityId", "Status", "GeneratedAt"];
        private static readonly string[] RuleFilterColumns = ["SensorType", "IsActive"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AttendedAt",
                table: "Alerts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttendedByUserId",
                table: "Alerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Alerts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "Alerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumValueSnapshot",
                table: "Alerts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumValueSnapshot",
                table: "Alerts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RuleId",
                table: "Alerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleName",
                table: "Alerts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Alerts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Alerts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.Sql("UPDATE Alerts SET Status = CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Closed' END, ClosedAt = ResolvedAt;");

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SensorType = table.Column<int>(type: "int", nullable: false),
                    MinimumValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AlertLevel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RiskType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CommunityId_Status_GeneratedAt",
                table: "Alerts",
                columns: AlertFilterColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_SensorType_IsActive",
                table: "AlertRules",
                columns: RuleFilterColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_CommunityId_Status_GeneratedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AttendedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AttendedByUserId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "MaximumValueSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "MinimumValueSnapshot",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "RuleId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "RuleName",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Alerts");
        }
    }
}
