using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutSession_SetInclineSpeedLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InclinePercent",
                table: "PerformedSets",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "PerformedSets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpeedKph",
                table: "PerformedSets",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InclinePercent",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "SpeedKph",
                table: "PerformedSets");
        }
    }
}
