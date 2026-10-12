using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations;

public partial class SingleMealSchedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing plans contain test data; reset them for the new single schedule.
        migrationBuilder.DropTable(name: "PlannedMeals");
        migrationBuilder.CreateTable(
            name: "MealScheduleEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Day = table.Column<int>(type: "int", nullable: false),
                Time = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MealScheduleEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_MealScheduleEntries_Meals_HouseholdId_MealId",
                    columns: x => new { x.HouseholdId, x.MealId },
                    principalTable: "Meals",
                    principalColumns: new[] { "HouseholdId", "Id" },
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(
            name: "IX_MealScheduleEntries_HouseholdId_Day_Time_MealId",
            table: "MealScheduleEntries",
            columns: new[] { "HouseholdId", "Day", "Time", "MealId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_MealScheduleEntries_HouseholdId_MealId",
            table: "MealScheduleEntries",
            columns: new[] { "HouseholdId", "MealId" });

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MealScheduleEntries");
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
            name: "IX_PlannedMeals_HouseholdId_Date_Time_MealId",
            table: "PlannedMeals",
            columns: new[] { "HouseholdId", "Date", "Time", "MealId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PlannedMeals_HouseholdId_MealId",
            table: "PlannedMeals",
            columns: new[] { "HouseholdId", "MealId" });
    }
}
