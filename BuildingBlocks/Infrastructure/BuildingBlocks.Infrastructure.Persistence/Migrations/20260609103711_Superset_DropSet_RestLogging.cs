using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Superset_DropSet_RestLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupersetGroupId",
                table: "PlanWorkoutExercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentSetId",
                table: "PerformedSets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersetGroupId",
                table: "PerformedExercises",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupersetGroupId",
                table: "PlanWorkoutExercises");

            migrationBuilder.DropColumn(
                name: "ParentSetId",
                table: "PerformedSets");

            migrationBuilder.DropColumn(
                name: "SupersetGroupId",
                table: "PerformedExercises");
        }
    }
}
