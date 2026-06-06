using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutSession_SnapshotJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Postgres has no automatic text -> jsonb cast; an explicit USING clause is required
            // (EF's AlterColumn omits it). Existing values already hold JSON, so the cast is safe.
            migrationBuilder.Sql(
                "ALTER TABLE \"WorkoutSessions\" ALTER COLUMN \"SnapshotJson\" TYPE jsonb USING \"SnapshotJson\"::jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"WorkoutSessions\" ALTER COLUMN \"SnapshotJson\" TYPE text USING \"SnapshotJson\"::text;");
        }
    }
}
