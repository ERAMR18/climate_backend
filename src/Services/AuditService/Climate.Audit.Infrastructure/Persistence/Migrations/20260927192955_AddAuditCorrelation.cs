using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");
        }
    }
}
