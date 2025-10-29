/*
  cleanup_migrations.sql
  - Lists applied migrations
  - Drops FK from SwapTransactions -> Invoices (if any)
  - Drops legacy FK FK_SwapTransactions_BatteryComplaints_ParentComplaintId1 (if any)
  - (Optional) Deletes a migration row from __EFMigrationsHistory (UNCOMMENT to run)

  IMPORTANT: Review before running. Prefer running the SELECT first to verify MigrationId.
*/

PRINT '1) List applied migrations (most recent first)';
SELECT MigrationId, ProductVersion
FROM [__EFMigrationsHistory]
ORDER BY MigrationId DESC;

PRINT '---';
PRINT '2) Attempt to drop any FK from SwapTransactions referencing Invoices (if present)';
DECLARE @fkName sysname;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.objects ro ON fk.referenced_object_id = ro.object_id
WHERE fk.parent_object_id = OBJECT_ID('SwapTransactions')
  AND ro.name = 'Invoices';

IF @fkName IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'ALTER TABLE [dbo].[SwapTransactions] DROP CONSTRAINT [' + @fkName + N']';
    PRINT 'Dropping FK: ' + @fkName;
    EXEC sp_executesql @sql;
END
ELSE
BEGIN
    PRINT 'No matching Invoice FK found on SwapTransactions.';
END

PRINT '---';
PRINT '3) Drop legacy FK FK_SwapTransactions_BatteryComplaints_ParentComplaintId1 (if present)';
DECLARE @fkNameOld sysname = 'FK_SwapTransactions_BatteryComplaints_ParentComplaintId1';
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @fkNameOld AND parent_object_id = OBJECT_ID('SwapTransactions'))
BEGIN
    EXEC('ALTER TABLE [dbo].[SwapTransactions] DROP CONSTRAINT [' + @fkNameOld + ']');
    PRINT 'Dropped OLD FK: ' + @fkNameOld;
END
ELSE
BEGIN
    PRINT 'OLD FK ParentComplaintId1 not found, clean.';
END

PRINT '---';
PRINT '4) OPTIONAL: Delete a migration row from __EFMigrationsHistory (uncomment to run)';
-- Replace the MigrationId below with the exact ID you want to remove, then remove the surrounding -- to execute.
-- DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251029160110_FinalComplaintFlowSetup';
-- PRINT 'Deleted migration row: 20251029160110_FinalComplaintFlowSetup';

PRINT 'Script finished.';
