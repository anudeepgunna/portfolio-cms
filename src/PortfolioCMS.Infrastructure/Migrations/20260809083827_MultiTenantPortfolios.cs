using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioCMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantPortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sections_Type",
                table: "Sections");

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Themes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Sections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Rows that predate multi-tenancy land on OwnerId 0, which is not a
            // real user and would fail the foreign keys added below. Hand them to
            // the first account — the original single-tenant owner. When Users is
            // empty the subquery is NULL, but so are these tables, so nothing runs.
            migrationBuilder.Sql("""
                UPDATE "Themes"   SET "OwnerId" = (SELECT MIN("Id") FROM "Users") WHERE "OwnerId" = 0;
                UPDATE "Sections" SET "OwnerId" = (SELECT MIN("Id") FROM "Users") WHERE "OwnerId" = 0;
                UPDATE "Projects" SET "OwnerId" = (SELECT MIN("Id") FROM "Users") WHERE "OwnerId" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Themes_OwnerId",
                table: "Themes",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_OwnerId_Type",
                table: "Sections",
                columns: new[] { "OwnerId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Users_OwnerId",
                table: "Sections",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Themes_Users_OwnerId",
                table: "Themes",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Users_OwnerId",
                table: "Sections");

            migrationBuilder.DropForeignKey(
                name: "FK_Themes_Users_OwnerId",
                table: "Themes");

            migrationBuilder.DropIndex(
                name: "IX_Themes_OwnerId",
                table: "Themes");

            migrationBuilder.DropIndex(
                name: "IX_Sections_OwnerId_Type",
                table: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_Type",
                table: "Sections",
                column: "Type",
                unique: true);
        }
    }
}
