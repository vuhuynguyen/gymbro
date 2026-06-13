using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Plan_DraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans");

            migrationBuilder.DropIndex(
                name: "IX_NutritionPlans_TemplateId_Version",
                table: "NutritionPlans");

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "WorkoutPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "NutritionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsDraft\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlans_TemplateId_Version",
                table: "NutritionPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsDraft\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans");

            migrationBuilder.DropIndex(
                name: "IX_NutritionPlans_TemplateId_Version",
                table: "NutritionPlans");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "NutritionPlans");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlans_TemplateId_Version",
                table: "NutritionPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
