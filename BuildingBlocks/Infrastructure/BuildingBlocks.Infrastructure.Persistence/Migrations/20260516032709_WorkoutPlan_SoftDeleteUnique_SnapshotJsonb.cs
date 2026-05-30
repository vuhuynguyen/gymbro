using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutPlan_SoftDeleteUnique_SnapshotJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans");

            migrationBuilder.Sql(
                """
                ALTER TABLE "PlanAssignments"
                ALTER COLUMN "SnapshotJson" TYPE jsonb
                USING CASE
                    WHEN "SnapshotJson" IS NULL OR btrim("SnapshotJson") = '' THEN NULL
                    ELSE "SnapshotJson"::jsonb
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans");

            migrationBuilder.Sql(
                """
                ALTER TABLE "PlanAssignments"
                ALTER COLUMN "SnapshotJson" TYPE character varying(32000)
                USING "SnapshotJson"::text;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_TemplateId_Version",
                table: "WorkoutPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true);
        }
    }
}
