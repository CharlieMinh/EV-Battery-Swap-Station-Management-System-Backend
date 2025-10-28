using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    public partial class AddRelatedComplaintIdToReservation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm cột mới vào bảng Reservations
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            // Tạo Index để tăng hiệu suất tìm kiếm
            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId");

            // Thêm Foreign Key Constraint liên kết tới bảng BatteryComplaints
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
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RelatedComplaintId",
                table: "Reservations");
        }
    }
}
