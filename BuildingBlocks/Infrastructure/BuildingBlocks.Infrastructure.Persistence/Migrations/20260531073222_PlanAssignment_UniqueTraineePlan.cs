using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanAssignment_UniqueTraineePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_TenantId_TraineeId_PlanId",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "TraineeId", "PlanId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanAssignments_TenantId_TraineeId_PlanId",
                table: "PlanAssignments");
        }
    }
}
