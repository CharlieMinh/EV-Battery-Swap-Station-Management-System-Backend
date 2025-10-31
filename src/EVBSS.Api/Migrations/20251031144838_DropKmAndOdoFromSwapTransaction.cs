using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropKmAndOdoFromSwapTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KmChargeAmount",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "VehicleOdoAtSwap",
                table: "SwapTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KmChargeAmount",
                table: "SwapTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VehicleOdoAtSwap",
                table: "SwapTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
