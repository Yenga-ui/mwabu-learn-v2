using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MinistryLessonForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "LessonRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "LessonRevisions");
        }
    }
}
