using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutPlans_TemplateLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "WorkoutPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"WorkoutPlans\" SET \"TemplateId\" = \"Id\" WHERE \"TemplateId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TemplateId",
                table: "WorkoutPlans",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "WorkoutPlans");
        }
    }
}
