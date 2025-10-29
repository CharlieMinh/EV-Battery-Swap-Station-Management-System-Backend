using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    public partial class RecreateComplaintRelationsClean : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add RelatedComplaintId column to SwapTransactions (nullable)
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            // Create index for RelatedComplaintId
            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions",
                column: "RelatedComplaintId");

            // Add FK to BatteryComplaints with SetNull on delete
            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions",
                column: "RelatedComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // NOTE: We intentionally do not attempt to drop legacy ParentComplaintId or ParentComplaintId1 columns here
            // to avoid accidental data-loss or failing the migration when those columns/constraints do not exist.
            // If you are certain those columns exist and should be removed, drop them manually or create a
            // targeted migration that conditionally removes them.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove FK
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions");

            // Drop index
            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions");

            // Drop column
            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "SwapTransactions");
        }
    }
}
