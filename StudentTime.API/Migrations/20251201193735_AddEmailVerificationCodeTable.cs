using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationCodeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailVerificationCodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<string>(type: "TEXT", nullable: false),
                    IsUsed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_Email_Code",
                table: "EmailVerificationCodes",
                columns: new[] { "Email", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_ExpiresAt",
                table: "EmailVerificationCodes",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationCodes");
        }
    }
}
