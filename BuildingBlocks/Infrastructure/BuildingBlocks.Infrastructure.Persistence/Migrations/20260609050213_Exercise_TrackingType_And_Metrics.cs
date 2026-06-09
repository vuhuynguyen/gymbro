using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Exercise_TrackingType_And_Metrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetDistanceM",
                table: "PlanWorkoutExerciseSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRounds",
                table: "PlanWorkoutExerciseSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvgHeartRate",
                table: "PerformedSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "PerformedSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rounds",
                table: "PerformedSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingType",
                table: "PerformedExercises",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TrackingType",
                table: "Exercises",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Backfill existing catalog rows to a sensible tracking mode instead of leaving everything Strength.
            // Mirrors ExerciseTrackingDefaults.Derive. Enum ints — Type: Strength=0, Cardio=1, Mobility=2,
            // Stretching=3; Equipment: Bodyweight=0; TrackingType: Strength=1, Bodyweight=2, Cardio=3, Mobility=6.
            migrationBuilder.Sql(@"UPDATE ""Exercises"" SET ""TrackingType"" = 3 WHERE ""Type"" = 1;");
            migrationBuilder.Sql(@"UPDATE ""Exercises"" SET ""TrackingType"" = 6 WHERE ""Type"" IN (2, 3);");
            migrationBuilder.Sql(@"UPDATE ""Exercises"" SET ""TrackingType"" = 2 WHERE ""Type"" = 0 AND ""Equipment"" = 0;");

            // Durable history: stamp already-logged performed exercises with their exercise's tracking mode.
            migrationBuilder.Sql(@"
                UPDATE ""PerformedExercises"" pe
                SET ""TrackingType"" = e.""TrackingType""
                FROM ""Exercises"" e
                WHERE pe.""ExerciseId"" = e.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetDistanceM",
                table: "PlanWorkoutExerciseSets");

            migrationBuilder.DropColumn(
                name: "TargetRounds",
                table: "PlanWorkoutExerciseSets");

            migrationBuilder.DropColumn(
                name: "AvgHeartRate",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "Rounds",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "TrackingType",
                table: "PerformedExercises");

            migrationBuilder.DropColumn(
                name: "TrackingType",
                table: "Exercises");
        }
    }
}
