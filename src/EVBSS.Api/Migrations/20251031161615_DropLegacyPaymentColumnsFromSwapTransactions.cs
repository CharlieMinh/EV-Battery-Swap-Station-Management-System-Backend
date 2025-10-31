using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyPaymentColumnsFromSwapTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_Users_BatteryIssuedByStaffId",
                table: "SwapTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_Users_BatteryReceivedByStaffId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_BatteryIssuedByStaffId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_BatteryReceivedByStaffId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "BatteryIssuedByStaffId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "BatteryReceivedByStaffId",
                table: "SwapTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatteryIssuedByStaffId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatteryReceivedByStaffId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_BatteryIssuedByStaffId",
                table: "SwapTransactions",
                column: "BatteryIssuedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_BatteryReceivedByStaffId",
                table: "SwapTransactions",
                column: "BatteryReceivedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_Users_BatteryIssuedByStaffId",
                table: "SwapTransactions",
                column: "BatteryIssuedByStaffId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_Users_BatteryReceivedByStaffId",
                table: "SwapTransactions",
                column: "BatteryReceivedByStaffId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
