using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentGenerationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentResultJson",
                table: "LessonGenerationJobs",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "LessonGenerationJobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "lesson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentResultJson",
                table: "LessonGenerationJobs");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "LessonGenerationJobs");
        }
    }
}
