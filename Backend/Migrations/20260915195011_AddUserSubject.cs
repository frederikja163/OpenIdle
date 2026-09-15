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

            // Accounts that predate external identities are all the shared debug account
            // (see UserService.TestUserSubject); give the row that subject so its profiles
            // stay reachable through LoginAsTestUser and the unique index can be created.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Subject\" = 'openidle-test-user' WHERE \"Subject\" = '';");

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
