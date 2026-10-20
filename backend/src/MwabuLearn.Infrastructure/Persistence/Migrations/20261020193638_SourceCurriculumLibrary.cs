using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceCurriculumLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCollection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurriculumYear = table.Column<int>(type: "integer", nullable: false),
                    ArchiveChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentImportBatches_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentImportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Issue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ParsedJson = table.Column<string>(type: "text", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentImportRecords_ContentImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ContentImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentImportRecords_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportedLessonTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumYear = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubTopic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Term = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedLessonTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedLessonTemplates_ContentImportRecords_ImportRecordId",
                        column: x => x.ImportRecordId,
                        principalTable: "ContentImportRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportedLessonTemplates_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportedLessonResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedLessonResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedLessonResources_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportedLessonResources_ContentAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "ContentAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportedLessonResources_ImportedLessonTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ImportedLessonTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportBatches_ActorUserId",
                table: "ContentImportBatches",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportBatches_SourceCollection_CurriculumYear_Archiv~",
                table: "ContentImportBatches",
                columns: new[] { "SourceCollection", "CurriculumYear", "ArchiveChecksum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportRecords_BatchId_SourcePath",
                table: "ContentImportRecords",
                columns: new[] { "BatchId", "SourcePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentImportRecords_ContentItemId",
                table: "ContentImportRecords",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonResources_AssetId",
                table: "ImportedLessonResources",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonResources_ReviewerId",
                table: "ImportedLessonResources",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonResources_TemplateId_AssetId",
                table: "ImportedLessonResources",
                columns: new[] { "TemplateId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonTemplates_ContentItemId",
                table: "ImportedLessonTemplates",
                column: "ContentItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonTemplates_CurriculumYear_Grade_Subject",
                table: "ImportedLessonTemplates",
                columns: new[] { "CurriculumYear", "Grade", "Subject" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonTemplates_CurriculumYear_OriginalChecksum",
                table: "ImportedLessonTemplates",
                columns: new[] { "CurriculumYear", "OriginalChecksum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedLessonTemplates_ImportRecordId",
                table: "ImportedLessonTemplates",
                column: "ImportRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedLessonResources");

            migrationBuilder.DropTable(
                name: "ImportedLessonTemplates");

            migrationBuilder.DropTable(
                name: "ContentImportRecords");

            migrationBuilder.DropTable(
                name: "ContentImportBatches");
        }
    }
}
