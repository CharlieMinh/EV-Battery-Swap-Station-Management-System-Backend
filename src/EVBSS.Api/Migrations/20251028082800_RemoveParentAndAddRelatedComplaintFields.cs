using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParentAndAddRelatedComplaintFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_ParentComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropColumn(
                name: "ParentComplaintId",
                table: "SwapTransactions");

            // SAFE-CLEANUP: Some environments have a shadow column ParentComplaintId1 created earlier by EF.
            // Ensure we drop it if it exists to avoid leftover columns.
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(@"
                    IF EXISTS (SELECT * FROM sys.columns 
                               WHERE Name = N'ParentComplaintId1' AND Object_ID = Object_ID(N'SwapTransactions'))
                    BEGIN
                        ALTER TABLE [SwapTransactions] DROP COLUMN [ParentComplaintId1];
                    END
                ");
            }

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedComplaintId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_BatteryComplaints_RelatedComplaintId",
                table: "Reservations",
                column: "RelatedComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
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

            migrationBuilder.AddColumn<Guid>(
                name: "ParentComplaintId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_ParentComplaintId",
                table: "SwapTransactions",
                column: "ParentComplaintId");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId",
                table: "SwapTransactions",
                column: "ParentComplaintId",
                principalTable: "BatteryComplaints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
