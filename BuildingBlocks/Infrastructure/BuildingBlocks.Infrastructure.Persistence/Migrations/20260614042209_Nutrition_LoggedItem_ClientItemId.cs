using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Nutrition_LoggedItem_ClientItemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientItemId",
                table: "LoggedItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoggedItems_DailyNutritionLogId_ClientItemId",
                table: "LoggedItems",
                columns: new[] { "DailyNutritionLogId", "ClientItemId" },
                unique: true,
                filter: "\"ClientItemId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoggedItems_DailyNutritionLogId_ClientItemId",
                table: "LoggedItems");

            migrationBuilder.DropColumn(
                name: "ClientItemId",
                table: "LoggedItems");
        }
    }
}
