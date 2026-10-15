using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BisiTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Operations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Round",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionPaise = table.Column<long>(type: "bigint", nullable: false),
                    StartMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Round", x => x.Id);
                    table.CheckConstraint("CK_Round_Contribution", "\"ContributionPaise\" > 0 AND \"ContributionPaise\" <= 100000000");
                    table.CheckConstraint("CK_Round_DueDay", "\"DueDay\" BETWEEN 1 AND 28");
                    table.ForeignKey(
                        name: "FK_Round_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Membership", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Membership_Round_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Round",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cycle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cycle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cycle_Membership_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Membership",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cycle_Round_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Round",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContributionDue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPaise = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionDue", x => x.Id);
                    table.CheckConstraint("CK_Due_Amount", "\"AmountPaise\" > 0");
                    table.ForeignKey(
                        name: "FK_ContributionDue_Cycle_CycleId",
                        column: x => x.CycleId,
                        principalTable: "Cycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContributionDue_Membership_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Membership",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayoutEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPaise = table.Column<long>(type: "bigint", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FundingNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutEntry", x => x.Id);
                    table.CheckConstraint("CK_Payout_Amount", "\"AmountPaise\" > 0");
                    table.ForeignKey(
                        name: "FK_PayoutEntry_Cycle_CycleId",
                        column: x => x.CycleId,
                        principalTable: "Cycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DueId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPaise = table.Column<long>(type: "bigint", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEntry", x => x.Id);
                    table.CheckConstraint("CK_Payment_Amount", "\"AmountPaise\" > 0");
                    table.ForeignKey(
                        name: "FK_PaymentEntry_ContributionDue_DueId",
                        column: x => x.DueId,
                        principalTable: "ContributionDue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReversal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReversal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReversal_PaymentEntry_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "PaymentEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_GroupId_RecordedAt",
                table: "AuditEvents",
                columns: new[] { "GroupId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionDue_CycleId_MemberId",
                table: "ContributionDue",
                columns: new[] { "CycleId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContributionDue_MemberId",
                table: "ContributionDue",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Cycle_RecipientId",
                table: "Cycle",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Cycle_RoundId_Number",
                table: "Cycle",
                columns: new[] { "RoundId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cycle_RoundId_RecipientId",
                table: "Cycle",
                columns: new[] { "RoundId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_OwnerId",
                table: "Groups",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Membership_RoundId_Position",
                table: "Membership",
                columns: new[] { "RoundId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operations_GroupId",
                table: "Operations",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_OwnerId_OperationId",
                table: "Operations",
                columns: new[] { "OwnerId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntry_DueId",
                table: "PaymentEntry",
                column: "DueId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReversal_PaymentId",
                table: "PaymentReversal",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntry_CycleId",
                table: "PayoutEntry",
                column: "CycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Round_GroupId",
                table: "Round",
                column: "GroupId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "PaymentReversal");

            migrationBuilder.DropTable(
                name: "PayoutEntry");

            migrationBuilder.DropTable(
                name: "PaymentEntry");

            migrationBuilder.DropTable(
                name: "ContributionDue");

            migrationBuilder.DropTable(
                name: "Cycle");

            migrationBuilder.DropTable(
                name: "Membership");

            migrationBuilder.DropTable(
                name: "Round");

            migrationBuilder.DropTable(
                name: "Groups");
        }
    }
}
