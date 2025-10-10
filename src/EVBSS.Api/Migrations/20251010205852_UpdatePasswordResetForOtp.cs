using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVBSS.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePasswordResetForOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "PasswordResetTokens",
                newName: "OtpHash");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "PasswordResetTokens",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "PasswordResetTokens");

            migrationBuilder.RenameColumn(
                name: "OtpHash",
                table: "PasswordResetTokens",
                newName: "TokenHash");
        }
    }
}
