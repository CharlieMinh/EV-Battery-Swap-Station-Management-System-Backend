using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserVehicleSubscriptionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "Vehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId1",
                table: "UserSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "UserSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId1",
                table: "UserSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_UserId1",
                table: "Vehicles",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId1",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId1",
                table: "UserSubscriptions",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_VehicleId1",
                table: "UserSubscriptions",
                column: "VehicleId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId1",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId1",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_Users_UserId1",
                table: "UserSubscriptions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_Vehicles_VehicleId1",
                table: "UserSubscriptions",
                column: "VehicleId1",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Users_UserId1",
                table: "Vehicles",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_Users_UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_Vehicles_VehicleId1",
                table: "UserSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Users_UserId1",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_UserId1",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_VehicleId1",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "VehicleId1",
                table: "UserSubscriptions");
        }
    }
}
