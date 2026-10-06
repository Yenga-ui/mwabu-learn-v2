using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.CheckConstraint("CK_Collections_SortOrder", "\"SortOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDownloadable = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentItems", x => x.Id);
                    table.CheckConstraint("CK_ContentItems_Duration", "\"EstimatedDurationMinutes\" IS NULL OR \"EstimatedDurationMinutes\" > 0");
                    table.CheckConstraint("CK_ContentItems_PublishedAt", "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_ContentItems_SortOrder", "\"SortOrder\" >= 0");
                    table.CheckConstraint("CK_ContentItems_Status", "\"Status\" IN ('Draft', 'InReview', 'Published', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AssetType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsPendingDeletion = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAssets", x => x.Id);
                    table.CheckConstraint("CK_ContentAssets_AssetType", "\"AssetType\" IN ('Document', 'Audio', 'Video', 'Image', 'Animation', 'Package', 'Other')");
                    table.CheckConstraint("CK_ContentAssets_FileSize", "\"FileSizeBytes\" > 0");
                    table.CheckConstraint("CK_ContentAssets_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ContentAssets_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCollections", x => x.Id);
                    table.CheckConstraint("CK_ContentCollections_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ContentCollections_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCollections_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentCurriculumMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    GradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompetencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LearningOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCurriculumMappings", x => x.Id);
                    table.CheckConstraint("CK_ContentCurriculumMappings_OneTarget", "CASE WHEN \"CurriculumVersionId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"GradeId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"SubjectId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"TermId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"TopicId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"CompetencyId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"LearningOutcomeId\" IS NOT NULL THEN 1 ELSE 0 END = 1");
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_Competencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "Competencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_CurriculumVersions_CurriculumVers~",
                        column: x => x.CurriculumVersionId,
                        principalTable: "CurriculumVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_LearningOutcomes_LearningOutcomeId",
                        column: x => x.LearningOutcomeId,
                        principalTable: "LearningOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentCurriculumMappings_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTags_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_IsActive_SortOrder",
                table: "Collections",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_NormalizedName",
                table: "Collections",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Slug",
                table: "Collections",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentAssets_ContentItemId",
                table: "ContentAssets",
                column: "ContentItemId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ContentAssets_ContentItemId_SortOrder",
                table: "ContentAssets",
                columns: new[] { "ContentItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAssets_StorageKey",
                table: "ContentAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCollections_CollectionId_SortOrder",
                table: "ContentCollections",
                columns: new[] { "CollectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentCollections_ContentItemId_CollectionId",
                table: "ContentCollections",
                columns: new[] { "ContentItemId", "CollectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_CompetencyId",
                table: "ContentCurriculumMappings",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_CompetencyId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "CompetencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_CurriculumVersionId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "CurriculumVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_GradeId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "GradeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_LearningOutcomeId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "LearningOutcomeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_SubjectId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_TermId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_ContentItemId_TopicId",
                table: "ContentCurriculumMappings",
                columns: new[] { "ContentItemId", "TopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_CurriculumVersionId",
                table: "ContentCurriculumMappings",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_GradeId",
                table: "ContentCurriculumMappings",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_LearningOutcomeId",
                table: "ContentCurriculumMappings",
                column: "LearningOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_SubjectId",
                table: "ContentCurriculumMappings",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_TermId",
                table: "ContentCurriculumMappings",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCurriculumMappings_TopicId",
                table: "ContentCurriculumMappings",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_ContentType",
                table: "ContentItems",
                column: "ContentType");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_LanguageCode",
                table: "ContentItems",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_Slug",
                table: "ContentItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_SortOrder_Id",
                table: "ContentItems",
                columns: new[] { "SortOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_Status_ContentType_LanguageCode",
                table: "ContentItems",
                columns: new[] { "Status", "ContentType", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_UpdatedAt",
                table: "ContentItems",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTags_ContentItemId_TagId",
                table: "ContentTags",
                columns: new[] { "ContentItemId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTags_TagId",
                table: "ContentTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Slug",
                table: "Tags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentAssets");

            migrationBuilder.DropTable(
                name: "ContentCollections");

            migrationBuilder.DropTable(
                name: "ContentCurriculumMappings");

            migrationBuilder.DropTable(
                name: "ContentTags");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "ContentItems");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
