using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleIdToReservationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Thêm cột VehicleId vào bảng Reservations (nullable)
            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            // 2. Tạo FK tới Vehicles
            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Vehicles_VehicleId",
                table: "Reservations",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            // 3. Tạo index cho VehicleId
            migrationBuilder.CreateIndex(
                name: "IX_Reservations_VehicleId",
                table: "Reservations",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa FK trước
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Vehicles_VehicleId",
                table: "Reservations");

            // Xóa index
            migrationBuilder.DropIndex(
                name: "IX_Reservations_VehicleId",
                table: "Reservations");

            // Xóa cột
            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Reservations");
        }
    }
}
