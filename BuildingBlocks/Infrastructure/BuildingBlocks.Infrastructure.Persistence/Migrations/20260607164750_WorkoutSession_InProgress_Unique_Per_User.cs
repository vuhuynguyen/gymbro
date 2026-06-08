using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutSession_InProgress_Unique_Per_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_TenantId_TraineeId_InProgress",
                table: "WorkoutSessions");

            // Data cleanup BEFORE the new per-user unique index can build: the old index allowed one
            // in-progress session per (tenant, trainee), so a user training in two gyms could hold two
            // active sessions. The new index permits only one in-progress session per user, so abandon
            // every in-progress session except each user's most recently started one (status 3 = Abandoned;
            // logged sets are preserved, mirroring a normal abandon). Idempotent and a no-op when no user
            // has more than one active session.
            migrationBuilder.Sql(
                """
                UPDATE "WorkoutSessions" SET "Status" = 3
                WHERE "Status" = 1
                  AND "Id" NOT IN (
                      SELECT DISTINCT ON ("TraineeId") "Id"
                      FROM "WorkoutSessions"
                      WHERE "Status" = 1
                      ORDER BY "TraineeId", "StartedAt" DESC
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_TraineeId_InProgress",
                table: "WorkoutSessions",
                column: "TraineeId",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: the Up data cleanup (abandoning duplicate in-progress sessions) is intentionally NOT
            // reversed — those sessions stay Abandoned. Only the index shape is restored.
            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_TraineeId_InProgress",
                table: "WorkoutSessions");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_TenantId_TraineeId_InProgress",
                table: "WorkoutSessions",
                columns: new[] { "TenantId", "TraineeId" },
                unique: true,
                filter: "\"Status\" = 1");
        }
    }
}
