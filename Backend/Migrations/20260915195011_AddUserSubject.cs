using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Keep one legacy account reachable through LoginAsTestUser, while assigning
            // every other account a stable, distinct subject before creating the unique index.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "Subject" = CASE
                    WHEN "UserId" = (SELECT MIN("UserId") FROM "Users")
                        THEN 'openidle-test-user'
                    ELSE 'openidle-legacy-user-' || "UserId"
                END
                WHERE "Subject" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Subject",
                table: "Users",
                column: "Subject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Subject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Users");
        }
    }
}
