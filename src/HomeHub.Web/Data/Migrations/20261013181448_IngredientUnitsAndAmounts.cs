using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IngredientUnitsAndAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Ingredients",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "Ingredients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IngredientUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientUnits", x => x.Id);
                    table.UniqueConstraint("AK_IngredientUnits_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.ForeignKey(
                        name: "FK_IngredientUnits_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing quantities count packets. Keep their meaning by assigning 1 packet per ingredient.
            migrationBuilder.Sql("""
                INSERT INTO [IngredientUnits] ([Id], [HouseholdId], [Name], [NormalizedName])
                SELECT NEWID(), [Id], N'packets', N'PACKETS' FROM [Households];
                UPDATE i SET [UnitId] = u.[Id]
                FROM [Ingredients] i JOIN [IngredientUnits] u ON i.[HouseholdId] = u.[HouseholdId];
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Ingredients",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_HouseholdId_UnitId",
                table: "Ingredients",
                columns: new[] { "HouseholdId", "UnitId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingredients_Amount",
                table: "Ingredients",
                sql: "[Amount] > 0 AND [Amount] <= 99999999.9999");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnits_HouseholdId_NormalizedName",
                table: "IngredientUnits",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_IngredientUnits_HouseholdId_UnitId",
                table: "Ingredients",
                columns: new[] { "HouseholdId", "UnitId" },
                principalTable: "IngredientUnits",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_IngredientUnits_HouseholdId_UnitId",
                table: "Ingredients");

            migrationBuilder.DropTable(
                name: "IngredientUnits");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_HouseholdId_UnitId",
                table: "Ingredients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ingredients_Amount",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "Ingredients");
        }
    }
}
