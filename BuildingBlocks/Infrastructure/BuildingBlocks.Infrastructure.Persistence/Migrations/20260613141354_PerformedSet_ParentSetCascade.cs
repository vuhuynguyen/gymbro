using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformedSet_ParentSetCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PerformedSets_ParentSetId",
                table: "PerformedSets",
                column: "ParentSetId");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformedSets_PerformedSets_ParentSetId",
                table: "PerformedSets",
                column: "ParentSetId",
                principalTable: "PerformedSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerformedSets_PerformedSets_ParentSetId",
                table: "PerformedSets");

            migrationBuilder.DropIndex(
                name: "IX_PerformedSets_ParentSetId",
                table: "PerformedSets");
        }
    }
}
