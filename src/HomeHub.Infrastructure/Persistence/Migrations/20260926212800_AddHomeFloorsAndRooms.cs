using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeFloorsAndRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HomeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeFloors_Homes_HomeId",
                        column: x => x.HomeId,
                        principalTable: "Homes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeRooms_HomeFloors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "HomeFloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeFloors_HomeId",
                table: "HomeFloors",
                column: "HomeId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeRooms_FloorId",
                table: "HomeRooms",
                column: "FloorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeRooms");

            migrationBuilder.DropTable(
                name: "HomeFloors");
        }
    }
}
