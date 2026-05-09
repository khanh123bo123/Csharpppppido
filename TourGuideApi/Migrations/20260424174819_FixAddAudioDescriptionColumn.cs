using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourGuideApi.Migrations
{
    /// <inheritdoc />
    public partial class FixAddAudioDescriptionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Locations"
                ADD COLUMN IF NOT EXISTS "AudioDescription" text NOT NULL DEFAULT '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Locations"
                DROP COLUMN IF EXISTS "AudioDescription";
                """);
        }
    }
}
