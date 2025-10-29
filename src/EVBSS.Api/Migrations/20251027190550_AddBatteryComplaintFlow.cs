using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryComplaintFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatteryComplaint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwapTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ComplaintDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HandledByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryComplaint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryComplaint_BatteryUnits_IssuedBatteryId",
                        column: x => x.IssuedBatteryId,
                        principalTable: "BatteryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryComplaint_SwapTransactions_SwapTransactionId",
                        column: x => x.SwapTransactionId,
                        principalTable: "SwapTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryComplaint_Users_HandledByStaffId",
                        column: x => x.HandledByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BatteryComplaint_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaint_HandledByStaffId",
                table: "BatteryComplaint",
                column: "HandledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaint_IssuedBatteryId",
                table: "BatteryComplaint",
                column: "IssuedBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaint_ReportedByUserId",
                table: "BatteryComplaint",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryComplaint_SwapTransactionId",
                table: "BatteryComplaint",
                column: "SwapTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatteryComplaint");
        }
    }
}
