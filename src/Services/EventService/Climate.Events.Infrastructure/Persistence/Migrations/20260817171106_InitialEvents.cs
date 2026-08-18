using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Events.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialEvents : Migration
    {
        private static readonly string[] CommunityDateColumns = ["CommunityId", "OccurredAt"];
        private static readonly string[] RiskLevelDateColumns = ["RiskType", "AlertLevel", "OccurredAt"];
        private static readonly string[] SensorDateColumns = ["SensorId", "OccurredAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClimateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AlertLevel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClimateEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClimateEvents_AlertId",
                table: "ClimateEvents",
                column: "AlertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClimateEvents_CommunityId_OccurredAt",
                table: "ClimateEvents",
                columns: CommunityDateColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ClimateEvents_RiskType_AlertLevel_OccurredAt",
                table: "ClimateEvents",
                columns: RiskLevelDateColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ClimateEvents_SensorId_OccurredAt",
                table: "ClimateEvents",
                columns: SensorDateColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClimateEvents");
        }
    }
}
