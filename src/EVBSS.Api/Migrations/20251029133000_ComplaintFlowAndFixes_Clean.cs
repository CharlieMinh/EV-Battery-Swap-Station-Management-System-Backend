using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    public partial class ComplaintFlowAndFixes_Clean : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create BatteryComplaints table (minimal safe schema)
            migrationBuilder.CreateTable(
                name: "BatteryComplaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedSwapTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryComplaints", x => x.Id);
                });

            // Add RelatedComplaintId to SwapTransactions
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions",
                column: "RelatedComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions",
                column: "RelatedComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Add RelatedComplaintId to Reservations
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove FKs and columns from Reservations
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "Reservations");

            // Remove FKs and columns from SwapTransactions
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "SwapTransactions");

            // Drop BatteryComplaints table
            migrationBuilder.DropTable(
                name: "BatteryComplaints");
        }
    }
}
