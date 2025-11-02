using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryStockRequestFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatteryStockRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatteryModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StaffNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdminReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RelatedBulkCreateRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryStockRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryStockRequests_BatteryModels_BatteryModelId",
                        column: x => x.BatteryModelId,
                        principalTable: "BatteryModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryStockRequests_BulkCreateRequests_RelatedBulkCreateRequestId",
                        column: x => x.RelatedBulkCreateRequestId,
                        principalTable: "BulkCreateRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryStockRequests_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryStockRequests_Users_AdminReviewerId",
                        column: x => x.AdminReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryStockRequests_Users_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_AdminReviewerId",
                table: "BatteryStockRequests",
                column: "AdminReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_BatteryModelId",
                table: "BatteryStockRequests",
                column: "BatteryModelId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_RelatedBulkCreateRequestId",
                table: "BatteryStockRequests",
                column: "RelatedBulkCreateRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_RequestedByStaffId",
                table: "BatteryStockRequests",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_StationId",
                table: "BatteryStockRequests",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStockRequests_Status_RequestDate",
                table: "BatteryStockRequests",
                columns: new[] { "Status", "RequestDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatteryStockRequests");
        }
    }
}
