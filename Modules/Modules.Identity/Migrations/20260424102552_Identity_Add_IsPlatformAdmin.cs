using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.IdentityModule.Migrations
{
    /// <inheritdoc />
    public partial class Identity_Add_IsPlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "AspNetUsers");
        }
    }
}
