using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdMemberProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Homes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.AddColumn<Guid>(
                name: "MemberId",
                table: "HomeMemberships",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "Revoked",
                table: "HomeInvitations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HomeMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HomeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    IsAccountLinked = table.Column<bool>(type: "bit", nullable: false),
                    IsRetired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeMembers_Homes_HomeId",
                        column: x => x.HomeId,
                        principalTable: "Homes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Give each existing membership a stable profile before enforcing its foreign key.
            migrationBuilder.Sql("""
                UPDATE [HomeMemberships] SET [MemberId] = NEWID();
                INSERT INTO [HomeMembers] ([Id], [HomeId], [DisplayName], [Colour], [IsAccountLinked], [IsRetired])
                SELECT m.[MemberId], m.[HomeId],
                    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(u.[UserName])), ''), 'Member'), 100),
                    NULL, 1, 0
                FROM [HomeMemberships] m
                INNER JOIN [AspNetUsers] u ON u.[Id] = m.[UserId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_HomeMemberships_MemberId",
                table: "HomeMemberships",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeMembers_HomeId",
                table: "HomeMembers",
                column: "HomeId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeMemberships_HomeMembers_MemberId",
                table: "HomeMemberships",
                column: "MemberId",
                principalTable: "HomeMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeMemberships_HomeMembers_MemberId",
                table: "HomeMemberships");

            migrationBuilder.DropTable(
                name: "HomeMembers");

            migrationBuilder.DropIndex(
                name: "IX_HomeMemberships_MemberId",
                table: "HomeMemberships");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Homes");

            migrationBuilder.DropColumn(
                name: "MemberId",
                table: "HomeMemberships");

            migrationBuilder.DropColumn(
                name: "Revoked",
                table: "HomeInvitations");
        }
    }
}
