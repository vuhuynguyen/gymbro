using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Nutrition_Food_Plan_Log : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyNutritionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClientTimezone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    NutritionPlanAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdherencePct = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_DailyNutritionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Foods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ServingLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServingSizeGrams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    EnergyKcal = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ProteinG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CarbsG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FatG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FiberG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Foods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NutritionPlanAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanVersion = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VisibilityMode = table.Column<int>(type: "integer", nullable: false),
                    HideMacroTargets = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisableTraineeEditing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_NutritionPlanAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NutritionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_NutritionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoggedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyNutritionLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanMealItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MealName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstitutedFromFoodId = table.Column<Guid>(type: "uuid", nullable: true),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServingLabelSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    EnergyKcal = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ProteinG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CarbsG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FatG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FiberG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LoggedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LoggedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoggedItems_DailyNutritionLogs_DailyNutritionLogId",
                        column: x => x.DailyNutritionLogId,
                        principalTable: "DailyNutritionLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoggedItems_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NutritionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DayApplicability = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PlanMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanMeals_NutritionPlans_NutritionPlanId",
                        column: x => x.NutritionPlanId,
                        principalTable: "NutritionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanMealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServingLabelSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EnergyKcal = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ProteinG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CarbsG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FatG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FiberG = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_PlanMealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanMealItems_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanMealItems_PlanMeals_PlanMealId",
                        column: x => x.PlanMealId,
                        principalTable: "PlanMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyNutritionLogs_TenantId_TraineeId_LocalDate",
                table: "DailyNutritionLogs",
                columns: new[] { "TenantId", "TraineeId", "LocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyNutritionLogs_TraineeId_LocalDate",
                table: "DailyNutritionLogs",
                columns: new[] { "TraineeId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Foods_TenantId_Kind",
                table: "Foods",
                columns: new[] { "TenantId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Foods_TenantId_Name",
                table: "Foods",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_LoggedItems_DailyNutritionLogId_Order",
                table: "LoggedItems",
                columns: new[] { "DailyNutritionLogId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_LoggedItems_DailyNutritionLogId_Status",
                table: "LoggedItems",
                columns: new[] { "DailyNutritionLogId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoggedItems_FoodId",
                table: "LoggedItems",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlanAssignments_TenantId_PlanId",
                table: "NutritionPlanAssignments",
                columns: new[] { "TenantId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlanAssignments_TenantId_TraineeId",
                table: "NutritionPlanAssignments",
                columns: new[] { "TenantId", "TraineeId" });

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlanAssignments_TenantId_TraineeId_PlanId",
                table: "NutritionPlanAssignments",
                columns: new[] { "TenantId", "TraineeId", "PlanId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlans_TemplateId_Version",
                table: "NutritionPlans",
                columns: new[] { "TemplateId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionPlans_TenantId_Name",
                table: "NutritionPlans",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanMealItems_FoodId",
                table: "PlanMealItems",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanMealItems_PlanMealId_Order",
                table: "PlanMealItems",
                columns: new[] { "PlanMealId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanMeals_NutritionPlanId_Order",
                table: "PlanMeals",
                columns: new[] { "NutritionPlanId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoggedItems");

            migrationBuilder.DropTable(
                name: "NutritionPlanAssignments");

            migrationBuilder.DropTable(
                name: "PlanMealItems");

            migrationBuilder.DropTable(
                name: "DailyNutritionLogs");

            migrationBuilder.DropTable(
                name: "Foods");

            migrationBuilder.DropTable(
                name: "PlanMeals");

            migrationBuilder.DropTable(
                name: "NutritionPlans");
        }
    }
}
