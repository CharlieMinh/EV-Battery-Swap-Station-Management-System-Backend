using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInvoiceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonthlyFeeUnder1500Km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyFee1500To3000Km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyFeeOver3000Km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BatteryModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingCycleDay = table.Column<int>(type: "int", nullable: false),
                    OverdueInterestRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    MaxOverdueMonths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_BatteryModels_BatteryModelId",
                        column: x => x.BatteryModelId,
                        principalTable: "BatteryModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentBillingPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentBillingPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentMonthKmUsed = table.Column<int>(type: "int", nullable: false),
                    DepositPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositPaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsecutiveOverdueMonths = table.Column<int>(type: "int", nullable: false),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    ChargingLimitPercent = table.Column<int>(type: "int", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KmUsedInPeriod = table.Column<int>(type: "int", nullable: true),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VnpTxnRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnpTransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnpResponseCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnpSecureHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnpPayDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_ProcessedByStaffId",
                        column: x => x.ProcessedByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SwapTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedBatterySerial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReturnedBatterySerial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckedInByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatteryIssuedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatteryReceivedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleOdoAtSwap = table.Column<int>(type: "int", nullable: false),
                    BatteryHealthIssued = table.Column<int>(type: "int", nullable: true),
                    BatteryHealthReturned = table.Column<int>(type: "int", nullable: true),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    SwapFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KmChargeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatteryIssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatteryReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwapTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_BatteryUnits_IssuedBatteryId",
                        column: x => x.IssuedBatteryId,
                        principalTable: "BatteryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_BatteryUnits_ReturnedBatteryId",
                        column: x => x.ReturnedBatteryId,
                        principalTable: "BatteryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Users_BatteryIssuedByStaffId",
                        column: x => x.BatteryIssuedByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Users_BatteryReceivedByStaffId",
                        column: x => x.BatteryReceivedByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Users_CheckedInByStaffId",
                        column: x => x.CheckedInByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Users_CompletedByStaffId",
                        column: x => x.CompletedByStaffId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwapTransactions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserId",
                table: "Invoices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserSubscriptionId",
                table: "Invoices",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentReference",
                table: "Payments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProcessedByStaffId",
                table: "Payments",
                column: "ProcessedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StationId",
                table: "Payments",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_BatteryModelId",
                table: "SubscriptionPlans",
                column: "BatteryModelId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_BatteryIssuedByStaffId",
                table: "SwapTransactions",
                column: "BatteryIssuedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_BatteryReceivedByStaffId",
                table: "SwapTransactions",
                column: "BatteryReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_CheckedInByStaffId",
                table: "SwapTransactions",
                column: "CheckedInByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_CompletedByStaffId",
                table: "SwapTransactions",
                column: "CompletedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_InvoiceId",
                table: "SwapTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_IssuedBatteryId",
                table: "SwapTransactions",
                column: "IssuedBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_ReservationId",
                table: "SwapTransactions",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_ReturnedBatteryId",
                table: "SwapTransactions",
                column: "ReturnedBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_StationId",
                table: "SwapTransactions",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_TransactionNumber",
                table: "SwapTransactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_UserId",
                table: "SwapTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_UserSubscriptionId",
                table: "SwapTransactions",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SwapTransactions_VehicleId",
                table: "SwapTransactions",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_VehicleId_IsActive",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "VehicleId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_VehicleId",
                table: "UserSubscriptions",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "SwapTransactions");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");
        }
    }
}
