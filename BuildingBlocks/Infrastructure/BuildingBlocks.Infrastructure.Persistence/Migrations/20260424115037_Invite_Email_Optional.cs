using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Invite_Email_Optional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invites_Email_TenantId",
                table: "Invites");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Invites",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Email_TenantId",
                table: "Invites",
                columns: new[] { "Email", "TenantId" },
                unique: true,
                filter: "\"IsUsed\" = false AND \"Email\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invites_Email_TenantId",
                table: "Invites");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Invites",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Email_TenantId",
                table: "Invites",
                columns: new[] { "Email", "TenantId" },
                unique: true,
                filter: "\"IsUsed\" = false");
        }
    }
}
