using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    public partial class RemoveLegacyParentComplaintColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Execute T-SQL that conditionally drops FK/index/column if they exist to avoid errors
            var sql = @"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ParentComplaintId' AND object_id = OBJECT_ID(N'dbo.SwapTransactions'))
BEGIN
    DECLARE @fkName NVARCHAR(200);
    SELECT TOP 1 @fkName = fk.name
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.SwapTransactions') AND c.name = 'ParentComplaintId';
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE dbo.SwapTransactions DROP CONSTRAINT [' + @fkName + ']');
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE name = 'IX_SwapTransactions_ParentComplaintId' AND object_id = OBJECT_ID(N'dbo.SwapTransactions'))
        EXEC('DROP INDEX IX_SwapTransactions_ParentComplaintId ON dbo.SwapTransactions');
    EXEC('ALTER TABLE dbo.SwapTransactions DROP COLUMN ParentComplaintId');
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ParentComplaintId1' AND object_id = OBJECT_ID(N'dbo.SwapTransactions'))
BEGIN
    DECLARE @fkName2 NVARCHAR(200);
    SELECT TOP 1 @fkName2 = fk.name
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.SwapTransactions') AND c.name = 'ParentComplaintId1';
    IF @fkName2 IS NOT NULL
        EXEC('ALTER TABLE dbo.SwapTransactions DROP CONSTRAINT [' + @fkName2 + ']');
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE name = 'IX_SwapTransactions_ParentComplaintId1' AND object_id = OBJECT_ID(N'dbo.SwapTransactions'))
        EXEC('DROP INDEX IX_SwapTransactions_ParentComplaintId1 ON dbo.SwapTransactions');
    EXEC('ALTER TABLE dbo.SwapTransactions DROP COLUMN ParentComplaintId1');
END
";

            migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the legacy columns as nullable GUIDs. We intentionally do NOT recreate FKs/indexes here
            // because restoring them correctly may require additional context or data.
            migrationBuilder.AddColumn<Guid>(
                name: "ParentComplaintId",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentComplaintId1",
                table: "SwapTransactions",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
