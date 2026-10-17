using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuardHouseholdItemUsageEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "HouseholdItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdItems_ExpectedWeeklyUsage",
                table: "HouseholdItems",
                sql: "[ExpectedWeeklyUsage] IS NULL OR ([ExpectedWeeklyUsage] > 0 AND [ExpectedWeeklyUsage] <= 99999999.9999)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdItems_ExpectedWeeklyUsage",
                table: "HouseholdItems");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "HouseholdItems");
        }
    }
}
