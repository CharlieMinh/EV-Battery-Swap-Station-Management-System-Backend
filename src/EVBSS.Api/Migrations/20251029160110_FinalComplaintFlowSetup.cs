using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinalComplaintFlowSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration previously attempted to recreate many tables which are already
            // created in the initial migration. To avoid duplicate-object errors when applying
            // migrations to a fresh DB we only apply the actual deltas introduced by this
            // migration: BatteryComplaints, BulkCreateRequests, Notifications and the
            // RelatedComplaintId on Reservations.

            migrationBuilder.CreateTable(
                name: "BatteryComplaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwapTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ComplaintDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HandledByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryComplaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryComplaints_BatteryUnits_IssuedBatteryId",
                        column: x => x.IssuedBatteryId,
                        principalTable: "BatteryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryComplaints_Users_HandledByStaffId",
                        column: x => x.HandledByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BatteryComplaints_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // The BulkCreateRequests and Notifications tables are created by separate migrations
            // earlier in the sequence. Do not recreate them here to avoid duplicate-object errors.

            // Add RelatedComplaintId to Reservations instead of recreating Reservations table
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaints_HandledByStaffId",
                table: "BatteryComplaints",
                column: "HandledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaints_IssuedBatteryId",
                table: "BatteryComplaints",
                column: "IssuedBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaints_ReportedByUserId",
                table: "BatteryComplaints",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaints_SwapTransactionId",
                table: "BatteryComplaints",
                column: "SwapTransactionId",
                unique: true);

            // Only create indexes related to the new objects/columns introduced by this
            // migration. The rest of the indexes are created in their respective
            // migrations earlier in the chain to avoid duplicate-index errors.

            // Only keep the index that refers to the new RelatedComplaintId column.
            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_BatteryComplaints_SwapTransactions_SwapTransactionId",
                table: "BatteryComplaints",
                column: "SwapTransactionId",
                principalTable: "SwapTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only reverse the changes introduced by this migration (the new tables and the added column)
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "Reservations");

            // Only drop tables that were created by this migration. BulkCreateRequests and
            // Notifications are handled by their own migrations and should not be dropped here.
            migrationBuilder.DropTable(
                name: "BatteryComplaints");
        }
    }
}
