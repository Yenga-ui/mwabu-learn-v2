using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceDependencyReviewEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceDependency",
                table: "ImportedLessonResources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewEvidence",
                table: "ContentImportRecords",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "ContentImportRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportRecords_ReviewerId",
                table: "ContentImportRecords",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentImportRecords_AspNetUsers_ReviewerId",
                table: "ContentImportRecords",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentImportRecords_AspNetUsers_ReviewerId",
                table: "ContentImportRecords");

            migrationBuilder.DropIndex(
                name: "IX_ContentImportRecords_ReviewerId",
                table: "ContentImportRecords");

            migrationBuilder.DropColumn(
                name: "SourceDependency",
                table: "ImportedLessonResources");

            migrationBuilder.DropColumn(
                name: "ReviewEvidence",
                table: "ContentImportRecords");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "ContentImportRecords");
        }
    }
}
