using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryComplaintFlow_20251028 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration was generated to reconcile model snapshot changes.
            // To avoid conflicts with existing database objects (table renames that may already have been applied),
            // we apply only the non-destructive operations conditionally.

            // Add ParentComplaintId columns to SwapTransactions if they don't exist
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SwapTransactions', 'ParentComplaintId') IS NULL
BEGIN
    ALTER TABLE dbo.SwapTransactions ADD ParentComplaintId uniqueidentifier NULL;
END
IF COL_LENGTH('dbo.SwapTransactions', 'ParentComplaintId1') IS NULL
BEGIN
    ALTER TABLE dbo.SwapTransactions ADD ParentComplaintId1 uniqueidentifier NULL;
END
");

            // Create index and foreign key only if they do not already exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SwapTransactions_ParentComplaintId1' AND object_id = OBJECT_ID('dbo.SwapTransactions'))
BEGIN
    CREATE INDEX IX_SwapTransactions_ParentComplaintId1 ON dbo.SwapTransactions(ParentComplaintId1);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SwapTransactions_BatteryComplaints_ParentComplaintId1')
BEGIN
    ALTER TABLE dbo.SwapTransactions
    ADD CONSTRAINT FK_SwapTransactions_BatteryComplaints_ParentComplaintId1 FOREIGN KEY (ParentComplaintId1) REFERENCES dbo.BatteryComplaints(Id);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BatteryComplaints_BatteryUnits_IssuedBatteryId",
                table: "BatteryComplaints");

            migrationBuilder.DropForeignKey(
                name: "FK_BatteryComplaints_SwapTransactions_SwapTransactionId",
                table: "BatteryComplaints");

            migrationBuilder.DropForeignKey(
                name: "FK_BatteryComplaints_Users_HandledByStaffId",
                table: "BatteryComplaints");

            migrationBuilder.DropForeignKey(
                name: "FK_BatteryComplaints_Users_ReportedByUserId",
                table: "BatteryComplaints");

            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId1",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_ParentComplaintId1",
                table: "SwapTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BatteryComplaints",
                table: "BatteryComplaints");

            migrationBuilder.DropColumn(
                name: "ParentComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "ParentComplaintId1",
                table: "SwapTransactions");

            migrationBuilder.RenameTable(
                name: "BatteryComplaints",
                newName: "BatteryComplaint");

            migrationBuilder.RenameIndex(
                name: "IX_BatteryComplaints_SwapTransactionId",
                table: "BatteryComplaint",
                newName: "IX_BatteryComplaint_SwapTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_BatteryComplaints_ReportedByUserId",
                table: "BatteryComplaint",
                newName: "IX_BatteryComplaint_ReportedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BatteryComplaints_IssuedBatteryId",
                table: "BatteryComplaint",
                newName: "IX_BatteryComplaint_IssuedBatteryId");

            migrationBuilder.RenameIndex(
                name: "IX_BatteryComplaints_HandledByStaffId",
                table: "BatteryComplaint",
                newName: "IX_BatteryComplaint_HandledByStaffId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BatteryComplaint",
                table: "BatteryComplaint",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BatteryComplaint_BatteryUnits_IssuedBatteryId",
                table: "BatteryComplaint",
                column: "IssuedBatteryId",
                principalTable: "BatteryUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BatteryComplaint_SwapTransactions_SwapTransactionId",
                table: "BatteryComplaint",
                column: "SwapTransactionId",
                principalTable: "SwapTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BatteryComplaint_Users_HandledByStaffId",
                table: "BatteryComplaint",
                column: "HandledByStaffId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BatteryComplaint_Users_ReportedByUserId",
                table: "BatteryComplaint",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
