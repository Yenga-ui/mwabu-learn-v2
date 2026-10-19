using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ContentItems_Status",
                table: "ContentItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContentItems_Status",
                table: "ContentItems",
                sql: "\"Status\" IN ('Draft', 'InReview', 'Approved', 'Published', 'Archived')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ContentItems_Status",
                table: "ContentItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContentItems_Status",
                table: "ContentItems",
                sql: "\"Status\" IN ('Draft', 'InReview', 'Published', 'Archived')");
        }
    }
}
