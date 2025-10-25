using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkCreateRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulkCreateRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatteryModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandledByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkCreateRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulkCreateRequests_BatteryModels_BatteryModelId",
                        column: x => x.BatteryModelId,
                        principalTable: "BatteryModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulkCreateRequests_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulkCreateRequests_Users_HandledByStaffId",
                        column: x => x.HandledByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BulkCreateRequests_Users_RequestedByAdminId",
                        column: x => x.RequestedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkCreateRequests_BatteryModelId",
                table: "BulkCreateRequests",
                column: "BatteryModelId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkCreateRequests_HandledByStaffId",
                table: "BulkCreateRequests",
                column: "HandledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkCreateRequests_RequestedByAdminId",
                table: "BulkCreateRequests",
                column: "RequestedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkCreateRequests_StationId",
                table: "BulkCreateRequests",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkCreateRequests");
        }
    }
}
