using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                    table.UniqueConstraint("AK_Meals_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.ForeignKey(
                        name: "FK_Meals_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlannedMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedMeals_Meals_HouseholdId_MealId",
                        columns: x => new { x.HouseholdId, x.MealId },
                        principalTable: "Meals",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meals_HouseholdId_NormalizedName",
                table: "Meals",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_HouseholdId_Date_Time_MealId",
                table: "PlannedMeals",
                columns: new[] { "HouseholdId", "Date", "Time", "MealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_HouseholdId_MealId",
                table: "PlannedMeals",
                columns: new[] { "HouseholdId", "MealId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedMeals");

            migrationBuilder.DropTable(
                name: "Meals");
        }
    }
}
