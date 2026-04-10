using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourGuideApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLocationSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationSuggestions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApprovedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedLocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    AdminNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ImageData = table.Column<string>(type: "TEXT", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationSuggestions_Locations_CreatedLocationId",
                        column: x => x.CreatedLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LocationSuggestions_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LocationSuggestions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationSuggestions_ApprovedByUserId",
                table: "LocationSuggestions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSuggestions_CreatedLocationId",
                table: "LocationSuggestions",
                column: "CreatedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSuggestions_UserId",
                table: "LocationSuggestions",
                column: "UserId");
        }
    }
}
