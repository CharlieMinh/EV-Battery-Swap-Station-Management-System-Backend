using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwapTransactionRelatedComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId1",
                table: "SwapTransactions");

            migrationBuilder.RenameColumn(
                name: "ParentComplaintId1",
                table: "SwapTransactions",
                newName: "RelatedComplaintId");

            migrationBuilder.RenameIndex(
                name: "IX_SwapTransactions_ParentComplaintId1",
                table: "SwapTransactions",
                newName: "IX_SwapTransactions_RelatedComplaintId");

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

            // Add FK for RelatedComplaintId using NO ACTION on delete to avoid multiple cascade paths on SQL Server
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SwapTransactions_BatteryComplaints_RelatedComplaintId')
BEGIN
    ALTER TABLE [SwapTransactions]
    ADD CONSTRAINT [FK_SwapTransactions_BatteryComplaints_RelatedComplaintId]
    FOREIGN KEY ([RelatedComplaintId]) REFERENCES [BatteryComplaints]([Id]) ON DELETE NO ACTION;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_RelatedComplaintId",
                table: "SwapTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SwapTransactions_ParentComplaintId",
                table: "SwapTransactions");

            migrationBuilder.RenameColumn(
                name: "RelatedComplaintId",
                table: "SwapTransactions",
                newName: "ParentComplaintId1");

            migrationBuilder.RenameIndex(
                name: "IX_SwapTransactions_RelatedComplaintId",
                table: "SwapTransactions",
                newName: "IX_SwapTransactions_ParentComplaintId1");

            migrationBuilder.AddForeignKey(
                name: "FK_SwapTransactions_BatteryComplaints_ParentComplaintId1",
                table: "SwapTransactions",
                column: "ParentComplaintId1",
                principalTable: "BatteryComplaints",
                principalColumn: "Id");
        }
    }
}
