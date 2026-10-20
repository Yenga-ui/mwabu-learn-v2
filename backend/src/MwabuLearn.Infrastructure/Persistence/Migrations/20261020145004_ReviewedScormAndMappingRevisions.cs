using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewedScormAndMappingRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceContentItemId",
                table: "ContentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "ContentCurriculumMappings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "ContentCurriculumMappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScormApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScormApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScormApprovals_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScormApprovals_ContentAssets_ContentAssetId",
                        column: x => x.ContentAssetId,
                        principalTable: "ContentAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_SourceContentItemId",
                table: "ContentItems",
                column: "SourceContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ReviewerId",
                table: "ContentCurriculumMappings",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScormApprovals_ContentAssetId",
                table: "ScormApprovals",
                column: "ContentAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScormApprovals_ReviewerId",
                table: "ScormApprovals",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentCurriculumMappings_AspNetUsers_ReviewerId",
                table: "ContentCurriculumMappings",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentItems_ContentItems_SourceContentItemId",
                table: "ContentItems",
                column: "SourceContentItemId",
                principalTable: "ContentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentCurriculumMappings_AspNetUsers_ReviewerId",
                table: "ContentCurriculumMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentItems_ContentItems_SourceContentItemId",
                table: "ContentItems");

            migrationBuilder.DropTable(
                name: "ScormApprovals");

            migrationBuilder.DropIndex(
                name: "IX_ContentItems_SourceContentItemId",
                table: "ContentItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentCurriculumMappings_ReviewerId",
                table: "ContentCurriculumMappings");

            migrationBuilder.DropColumn(
                name: "SourceContentItemId",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "ContentCurriculumMappings");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "ContentCurriculumMappings");
        }
    }
}
