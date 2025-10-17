using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAllPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra cột Status đã tồn tại chưa trước khi thêm
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Status'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [Status] int NOT NULL DEFAULT 0;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa cột Status nếu tồn tại
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Status'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [Status];
                END
            ");
        }
    }
}
