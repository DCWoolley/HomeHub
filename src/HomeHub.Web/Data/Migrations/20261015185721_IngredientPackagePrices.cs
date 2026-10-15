using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IngredientPackagePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackagePrice",
                table: "Ingredients",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingredients_PackagePrice",
                table: "Ingredients",
                sql: "[PackagePrice] IS NULL OR ([PackagePrice] >= 0 AND [PackagePrice] <= 99999999.99)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ingredients_PackagePrice",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "PackagePrice",
                table: "Ingredients");
        }
    }
}
