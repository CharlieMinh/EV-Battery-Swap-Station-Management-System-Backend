using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryInventoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatteryInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatteryModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryInventories_BatteryModels_BatteryModelId",
                        column: x => x.BatteryModelId,
                        principalTable: "BatteryModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatteryInventories_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryInventories_BatteryModelId_StationId_Status",
                table: "BatteryInventories",
                columns: new[] { "BatteryModelId", "StationId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatteryInventories_StationId",
                table: "BatteryInventories",
                column: "StationId");

            // HYBRID SOLUTION: Sync initial data from BatteryUnits to BatteryInventory
            // This creates aggregated inventory records based on existing individual batteries
            migrationBuilder.Sql(@"
                INSERT INTO BatteryInventories (Id, BatteryModelId, StationId, Status, Quantity, UpdatedAt)
                SELECT 
                    NEWID() AS Id,
                    BatteryModelId,
                    StationId,
                    Status,
                    COUNT(*) AS Quantity,
                    GETUTCDATE() AS UpdatedAt
                FROM BatteryUnits
                GROUP BY BatteryModelId, StationId, Status
                HAVING COUNT(*) > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatteryInventories");
        }
    }
}
