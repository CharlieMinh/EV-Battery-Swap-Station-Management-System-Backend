using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingUserColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm AuthMethod nếu chưa tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'AuthMethod'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [AuthMethod] int NOT NULL DEFAULT 0;
                END
            ");

            // Thêm GoogleId nếu chưa tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'GoogleId'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [GoogleId] nvarchar(max) NULL;
                END
            ");

            // Thêm ProfilePictureUrl nếu chưa tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'ProfilePictureUrl'
                )
                BEGIN
                    ALTER TABLE [Users] ADD [ProfilePictureUrl] nvarchar(max) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa các cột nếu tồn tại
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'AuthMethod'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [AuthMethod];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'GoogleId'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [GoogleId];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'ProfilePictureUrl'
                )
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [ProfilePictureUrl];
                END
            ");
        }
    }
}
