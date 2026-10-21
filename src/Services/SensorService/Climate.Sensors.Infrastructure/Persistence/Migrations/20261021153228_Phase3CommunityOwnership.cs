using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Climate.Sensors.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3CommunityOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sensors_Communities_CommunityId",
                table: "Sensors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Sensors_Communities_CommunityId",
                table: "Sensors",
                column: "CommunityId",
                principalTable: "Communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
