using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReservationToSlotBased2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: First migration partially failed, need to add ALL missing columns
            // Check if columns exist before adding them to handle partially migrated state

            // Add columns that were successfully added in first attempt (they exist now)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'CancelNote')
                BEGIN
                    ALTER TABLE [Reservations] ADD [CancelNote] nvarchar(max) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'CancelReason')
                BEGIN
                    ALTER TABLE [Reservations] ADD [CancelReason] int NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'CancelledAt')
                BEGIN
                    ALTER TABLE [Reservations] ADD [CancelledAt] datetime2 NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'CheckedInAt')
                BEGIN
                    ALTER TABLE [Reservations] ADD [CheckedInAt] datetime2 NULL;
                END
            ");

            // Rename StartTime → SlotDate if needed
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'StartTime')
                AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'SlotDate')
                BEGIN
                    EXEC sp_rename 'Reservations.StartTime', 'SlotDate', 'COLUMN';
                END
            ");

            // Drop HoldDurationMinutes if it exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'HoldDurationMinutes')
                BEGIN
                    ALTER TABLE [Reservations] DROP COLUMN [HoldDurationMinutes];
                END
            ");

            // Make BatteryUnitId nullable if needed
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'BatteryUnitId')
                BEGIN
                    ALTER TABLE [Reservations] ALTER COLUMN [BatteryUnitId] uniqueidentifier NULL;
                END
            ");

            // Add columns that were NOT successfully added (failed at QRCode)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'QRCode')
                BEGIN
                    ALTER TABLE [Reservations] ADD [QRCode] nvarchar(max) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'SlotEndTime')
                BEGIN
                    ALTER TABLE [Reservations] ADD [SlotEndTime] time NOT NULL DEFAULT '00:00:00';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'SlotStartTime')
                BEGIN
                    ALTER TABLE [Reservations] ADD [SlotStartTime] time NOT NULL DEFAULT '00:00:00';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'VerifiedByStaffId')
                BEGIN
                    ALTER TABLE [Reservations] ADD [VerifiedByStaffId] uniqueidentifier NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND name = 'IX_Reservations_VerifiedByStaffId')
                BEGIN
                    CREATE INDEX [IX_Reservations_VerifiedByStaffId] ON [Reservations] ([VerifiedByStaffId]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Reservations_Users_VerifiedByStaffId]'))
                BEGIN
                    ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_Users_VerifiedByStaffId] 
                    FOREIGN KEY ([VerifiedByStaffId]) REFERENCES [Users] ([Id]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback only the columns we added in this migration
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Users_VerifiedByStaffId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_VerifiedByStaffId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "QRCode",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SlotEndTime",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SlotStartTime",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "VerifiedByStaffId",
                table: "Reservations");
        }
    }
}
