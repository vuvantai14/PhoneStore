using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail closed on legacy/manual drift; never repair roles or delete extra admins.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [RoleId] = 1 AND [RoleName] = N'Admin')
    OR EXISTS (SELECT 1 FROM [Roles] WHERE [RoleName] = N'Admin' AND [RoleId] <> 1)
    THROW 51000, 'Admin role mapping must be reviewed before this migration.', 1;
IF (SELECT COUNT_BIG(*) FROM [Users] WHERE [RoleId] = 1) > 1
    THROW 51001, 'Multiple Admin accounts exist; no automatic data changes are allowed.', 1;
");

            migrationBuilder.CreateIndex(
                name: "UX_Users_SingleAdmin",
                table: "Users",
                column: "RoleId",
                unique: true,
                filter: "[RoleId] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_AdminIdentity",
                table: "Roles",
                sql: "([RoleId] = 1 AND [RoleName] = 'Admin') OR ([RoleId] <> 1 AND [RoleName] <> 'Admin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_SingleAdmin",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_AdminIdentity",
                table: "Roles");

        }
    }
}
