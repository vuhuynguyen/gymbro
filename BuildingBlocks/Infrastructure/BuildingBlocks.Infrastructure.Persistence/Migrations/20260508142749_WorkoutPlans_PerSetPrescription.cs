using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutPlans_PerSetPrescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reps",
                table: "PlanWorkoutExercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "PlanWorkoutExercises");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "PlanWorkoutExercises");

            migrationBuilder.CreateTable(
                name: "PlanWorkoutExerciseSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanWorkoutExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SetType = table.Column<int>(type: "integer", nullable: false),
                    TargetReps = table.Column<int>(type: "integer", nullable: true),
                    TargetWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    TargetRpe = table.Column<int>(type: "integer", nullable: true),
                    TargetDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    RestSeconds = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanWorkoutExerciseSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanWorkoutExerciseSets_PlanWorkoutExercises_PlanWorkoutExe~",
                        column: x => x.PlanWorkoutExerciseId,
                        principalTable: "PlanWorkoutExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanWorkoutExerciseSets_PlanWorkoutExerciseId_Order",
                table: "PlanWorkoutExerciseSets",
                columns: new[] { "PlanWorkoutExerciseId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanWorkoutExerciseSets");

            migrationBuilder.AddColumn<int>(
                name: "Reps",
                table: "PlanWorkoutExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "PlanWorkoutExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "PlanWorkoutExercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
