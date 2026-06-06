using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutSession_PrCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrCount",
                table: "WorkoutSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill PrCount for existing completed sessions in one pass, using a window function to
            // replicate the chronological running-max e1RM walk the handler now does incrementally at
            // completion. Per (tenant, trainee, exercise), prior_best is the max session-best e1RM of all
            // strictly-earlier sessions (any status — an abandoned/in-progress lift still raises the bar);
            // a session scores a PR for each exercise whose session-best beats that prior best. Only
            // completed sessions are written; in-progress/abandoned rows keep the default 0.
            // SetType 2 = Working, Status 2 = Completed.
            migrationBuilder.Sql("""
                WITH session_ex_best AS (
                    SELECT s."Id"          AS session_id,
                           s."TenantId"    AS tenant_id,
                           s."TraineeId"   AS trainee_id,
                           s."StartedAt"   AS started_at,
                           pe."ExerciseId" AS exercise_id,
                           MAX(ps."EstimatedOneRepMaxKg") AS best
                    FROM "WorkoutSessions" s
                    JOIN "PerformedExercises" pe ON pe."SessionId" = s."Id"
                    JOIN "PerformedSets" ps ON ps."PerformedExerciseId" = pe."Id"
                    WHERE s."IsDeleted" = false
                      AND ps."SetType" = 2
                      AND ps."EstimatedOneRepMaxKg" IS NOT NULL
                    GROUP BY s."Id", s."TenantId", s."TraineeId", s."StartedAt", pe."ExerciseId"
                ),
                with_prior AS (
                    SELECT session_id,
                           best,
                           MAX(best) OVER (
                               PARTITION BY tenant_id, trainee_id, exercise_id
                               ORDER BY started_at
                               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                           ) AS prior_best
                    FROM session_ex_best
                ),
                pr AS (
                    SELECT session_id, COUNT(*) AS pr_count
                    FROM with_prior
                    WHERE prior_best IS NULL OR best > prior_best
                    GROUP BY session_id
                )
                UPDATE "WorkoutSessions" t
                SET "PrCount" = pr.pr_count
                FROM pr
                WHERE t."Id" = pr.session_id
                  AND t."Status" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrCount",
                table: "WorkoutSessions");
        }
    }
}
