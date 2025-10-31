using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdToSwapTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "SwapFee",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "SwapTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_PaymentId",
                table: "SwapTransactions",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_Payments_PaymentId",
                table: "SwapTransactions",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_Payments_PaymentId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_PaymentId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "SwapTransactions");

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "SwapTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "SwapTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SwapFee",
                table: "SwapTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "SwapTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
