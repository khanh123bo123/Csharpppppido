using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourGuideApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationPlayCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayCount",
                table: "Localizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayCount",
                table: "Localizations");
        }
    }
}
