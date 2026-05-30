using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutPlans_Assignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WorkoutPlans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PlanAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanVersion = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FrequencyDaysPerWeek = table.Column<int>(type: "integer", nullable: false),
                    VisibilityMode = table.Column<int>(type: "integer", nullable: false),
                    HideExercises = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HideSetsReps = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HideFutureWorkouts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisableTraineeEditing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SnapshotJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_TenantId_PlanId",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_TenantId_TraineeId",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "TraineeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanAssignments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkoutPlans");
        }
    }
}
