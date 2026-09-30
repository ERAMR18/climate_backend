using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Sensors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2OutboxAndCatalog : Migration
    {
        private static readonly string[] OutboxIndexColumns = ["PublishedAt", "CreatedAt"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnvironmentalType",
                table: "Sensors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InstallationDate",
                table: "Sensors",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Sensors",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Communities",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Communities",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                table: "Communities",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditOutbox_PublishedAt_CreatedAt",
                table: "AuditOutbox",
                columns: OutboxIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditOutbox");

            migrationBuilder.DropColumn(
                name: "EnvironmentalType",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "Municipality",
                table: "Communities");
        }
    }
}
