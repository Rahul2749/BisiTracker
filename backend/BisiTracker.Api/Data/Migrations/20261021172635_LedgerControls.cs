using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BisiTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LedgerControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Cycle",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayoutReversal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutReversal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutReversal_PayoutEntry_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "PayoutEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutReversal_PayoutId",
                table: "PayoutReversal",
                column: "PayoutId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutReversal");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Cycle");
        }
    }
}
