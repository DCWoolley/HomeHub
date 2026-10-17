using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpectedWeeklyUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedWeeklyUsage",
                table: "HouseholdItems",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedWeeklyUsage",
                table: "HouseholdItems");
        }
    }
}
