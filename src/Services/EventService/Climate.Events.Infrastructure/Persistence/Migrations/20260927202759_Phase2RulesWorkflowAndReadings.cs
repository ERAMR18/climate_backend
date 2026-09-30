using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Events.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2RulesWorkflowAndReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleUserId",
                table: "ClimateEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ClimateEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "ClimateEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
            migrationBuilder.Sql("UPDATE ClimateEvents SET Status = CASE WHEN ResolvedAt IS NULL THEN 'Active' ELSE 'Closed' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponsibleUserId",
                table: "ClimateEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ClimateEvents");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ClimateEvents");
        }
    }
}
