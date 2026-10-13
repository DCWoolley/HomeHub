using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MealRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.UniqueConstraint("AK_Ingredients_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.ForeignKey(
                        name: "FK_Ingredients_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeItems",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeItems", x => new { x.HouseholdId, x.MealId, x.IngredientId });
                    table.CheckConstraint("CK_RecipeItems_Quantity", "[Quantity] > 0 AND [Quantity] <= 99999999.9999");
                    table.ForeignKey(
                        name: "FK_RecipeItems_Ingredients_HouseholdId_IngredientId",
                        columns: x => new { x.HouseholdId, x.IngredientId },
                        principalTable: "Ingredients",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeItems_Meals_HouseholdId_MealId",
                        columns: x => new { x.HouseholdId, x.MealId },
                        principalTable: "Meals",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_HouseholdId_NormalizedName",
                table: "Ingredients",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItems_HouseholdId_IngredientId",
                table: "RecipeItems",
                columns: new[] { "HouseholdId", "IngredientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeItems");

            migrationBuilder.DropTable(
                name: "Ingredients");
        }
    }
}
