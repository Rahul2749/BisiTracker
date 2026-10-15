using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BisiTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupportPartialPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayoutEntry_CycleId",
                table: "PayoutEntry");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntry_CycleId",
                table: "PayoutEntry",
                column: "CycleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayoutEntry_CycleId",
                table: "PayoutEntry");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntry_CycleId",
                table: "PayoutEntry",
                column: "CycleId",
                unique: true);
        }
    }
}
