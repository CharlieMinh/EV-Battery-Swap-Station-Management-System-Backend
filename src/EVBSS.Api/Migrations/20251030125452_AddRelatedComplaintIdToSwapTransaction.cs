using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedComplaintIdToSwapTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "SwapTransactions");
        }
    }
}
