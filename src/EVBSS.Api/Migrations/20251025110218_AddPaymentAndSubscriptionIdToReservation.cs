using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAndSubscriptionIdToReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserSubscriptionId",
                table: "Reservations",
                column: "UserSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_UserSubscriptions_UserSubscriptionId",
                table: "Reservations",
                column: "UserSubscriptionId",
                principalTable: "UserSubscriptions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_UserSubscriptions_UserSubscriptionId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_UserSubscriptionId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "Reservations");
        }
    }
}
