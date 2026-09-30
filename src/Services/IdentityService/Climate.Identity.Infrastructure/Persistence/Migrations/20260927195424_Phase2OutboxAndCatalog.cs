using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2OutboxAndCatalog : Migration
    {
        private static readonly string[] OutboxIndexColumns = ["PublishedAt", "CreatedAt"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                table: "Users",
                type: "datetimeoffset",
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
                name: "LastLoginAt",
                table: "Users");
        }
    }
}
