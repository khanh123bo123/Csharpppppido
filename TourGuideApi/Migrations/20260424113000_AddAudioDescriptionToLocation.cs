using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourGuideApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioDescriptionToLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioDescription",
                table: "Locations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioDescription",
                table: "Locations");
        }
    }
}
