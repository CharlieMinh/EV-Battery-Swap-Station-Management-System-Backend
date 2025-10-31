using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EVBSS.Api.Migrations
{
    public partial class DropLegacyPaymentColumnsFromSwapTransactions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop legacy payment-related columns now normalized via Payments table
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "SwapFee",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "SwapTransactions");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate legacy columns for rollback
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "SwapTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SwapFee",
                table: "SwapTransactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "SwapTransactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "SwapTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}


