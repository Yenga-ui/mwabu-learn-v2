using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CurriculumManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize legacy values before applying length limits and scoped unique indexes.
            // Invalid lengths or existing duplicates intentionally fail rather than silently losing data.
            migrationBuilder.Sql("""
                UPDATE "Curricula" SET "Name" = btrim("Name"), "CountryCode" = upper(btrim("CountryCode"));
                UPDATE "Grades" SET "Name" = btrim("Name");
                UPDATE "Subjects" SET "Name" = btrim("Name"), "Code" = NULLIF(upper(btrim("Code")), '');
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Grades_Curricula_CurriculumId",
                table: "Grades");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Grades_GradeId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Grades_CurriculumId",
                table: "Grades");

            migrationBuilder.RenameColumn(
                name: "CurriculumId",
                table: "Grades",
                newName: "CurriculumVersionId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subjects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Subjects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Subjects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Subjects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Subjects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Grades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Grades",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Grades",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Grades",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Grades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Curricula",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Curricula",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Curricula",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Curricula",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Curricula",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Curricula",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CurriculumVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumVersions", x => x.Id);
                    table.CheckConstraint("CK_CurriculumVersions_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_CurriculumVersions_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                    table.CheckConstraint("CK_Terms_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_Terms_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.CheckConstraint("CK_Topics_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_Topics_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Competencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competencies", x => x.Id);
                    table.CheckConstraint("CK_Competencies_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_Competencies_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompetencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningOutcomes", x => x.Id);
                    table.CheckConstraint("CK_LearningOutcomes_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_LearningOutcomes_Competencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "Competencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Old grades had no version information. Preserve their IDs and attach them to an
            // explicitly unversioned legacy version; operators can rename it after review.
            // IDs can safely match across distinct tables, avoiding a database UUID extension.
            migrationBuilder.Sql("""
                UPDATE "Curricula" SET "NormalizedName" = upper("Name");
                UPDATE "Grades" SET "NormalizedName" = upper("Name");
                UPDATE "Subjects" SET "NormalizedName" = upper("Name");
                INSERT INTO "CurriculumVersions"
                    ("Id", "CurriculumId", "Name", "NormalizedName", "Code", "Description", "SortOrder", "IsActive", "CreatedAt")
                SELECT c."Id", c."Id", 'Legacy (unversioned)', 'LEGACY (UNVERSIONED)', 'LEGACY',
                    'Migrated grades: the original schema did not record a curriculum version.', 0, c."IsActive", CURRENT_TIMESTAMP
                FROM "Curricula" c
                WHERE EXISTS (SELECT 1 FROM "Grades" g WHERE g."CurriculumVersionId" = c."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId_Code",
                table: "Subjects",
                columns: new[] { "GradeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId_NormalizedName",
                table: "Subjects",
                columns: new[] { "GradeId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId_SortOrder",
                table: "Subjects",
                columns: new[] { "GradeId", "SortOrder" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_SortOrder",
                table: "Subjects",
                sql: "\"SortOrder\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_CurriculumVersionId_Code",
                table: "Grades",
                columns: new[] { "CurriculumVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_CurriculumVersionId_NormalizedName",
                table: "Grades",
                columns: new[] { "CurriculumVersionId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_CurriculumVersionId_SortOrder",
                table: "Grades",
                columns: new[] { "CurriculumVersionId", "SortOrder" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Grades_SortOrder",
                table: "Grades",
                sql: "\"SortOrder\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_CountryCode_Code",
                table: "Curricula",
                columns: new[] { "CountryCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_CountryCode_NormalizedName",
                table: "Curricula",
                columns: new[] { "CountryCode", "NormalizedName" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Curricula_SortOrder",
                table: "Curricula",
                sql: "\"SortOrder\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_TopicId_Code",
                table: "Competencies",
                columns: new[] { "TopicId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_TopicId_NormalizedName",
                table: "Competencies",
                columns: new[] { "TopicId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_TopicId_SortOrder",
                table: "Competencies",
                columns: new[] { "TopicId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumVersions_CurriculumId_Code",
                table: "CurriculumVersions",
                columns: new[] { "CurriculumId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumVersions_CurriculumId_NormalizedName",
                table: "CurriculumVersions",
                columns: new[] { "CurriculumId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumVersions_CurriculumId_SortOrder",
                table: "CurriculumVersions",
                columns: new[] { "CurriculumId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningOutcomes_CompetencyId_Code",
                table: "LearningOutcomes",
                columns: new[] { "CompetencyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningOutcomes_CompetencyId_NormalizedName",
                table: "LearningOutcomes",
                columns: new[] { "CompetencyId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningOutcomes_CompetencyId_SortOrder",
                table: "LearningOutcomes",
                columns: new[] { "CompetencyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Terms_SubjectId_Code",
                table: "Terms",
                columns: new[] { "SubjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terms_SubjectId_NormalizedName",
                table: "Terms",
                columns: new[] { "SubjectId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terms_SubjectId_SortOrder",
                table: "Terms",
                columns: new[] { "SubjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_TermId_Code",
                table: "Topics",
                columns: new[] { "TermId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_TermId_NormalizedName",
                table: "Topics",
                columns: new[] { "TermId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_TermId_SortOrder",
                table: "Topics",
                columns: new[] { "TermId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Grades_CurriculumVersions_CurriculumVersionId",
                table: "Grades",
                column: "CurriculumVersionId",
                principalTable: "CurriculumVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Grades_GradeId",
                table: "Subjects",
                column: "GradeId",
                principalTable: "Grades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grades_CurriculumVersions_CurriculumVersionId",
                table: "Grades");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Grades_GradeId",
                table: "Subjects");

            migrationBuilder.DropTable(
                name: "LearningOutcomes");

            migrationBuilder.DropTable(
                name: "Competencies");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId_Code",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId_NormalizedName",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId_SortOrder",
                table: "Subjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_SortOrder",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Grades_CurriculumVersionId_Code",
                table: "Grades");

            migrationBuilder.DropIndex(
                name: "IX_Grades_CurriculumVersionId_NormalizedName",
                table: "Grades");

            migrationBuilder.DropIndex(
                name: "IX_Grades_CurriculumVersionId_SortOrder",
                table: "Grades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Grades_SortOrder",
                table: "Grades");

            migrationBuilder.DropIndex(
                name: "IX_Curricula_CountryCode_Code",
                table: "Curricula");

            migrationBuilder.DropIndex(
                name: "IX_Curricula_CountryCode_NormalizedName",
                table: "Curricula");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Curricula_SortOrder",
                table: "Curricula");

            // Restore the original curriculum relationship after dropping scoped grade indexes,
            // since grades with the same name/code can exist in different versions.
            // A rollback loses version distinctions and the newly introduced hierarchy levels.
            migrationBuilder.Sql("""
                UPDATE "Grades" g SET "CurriculumVersionId" = v."CurriculumId"
                FROM "CurriculumVersions" v WHERE g."CurriculumVersionId" = v."Id";
                """);

            migrationBuilder.DropTable(name: "CurriculumVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Curricula");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Curricula");

            migrationBuilder.RenameColumn(
                name: "CurriculumVersionId",
                table: "Grades",
                newName: "CurriculumId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subjects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Subjects",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Subjects",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Grades",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Curricula",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Curricula",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Curricula",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId",
                table: "Subjects",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_CurriculumId",
                table: "Grades",
                column: "CurriculumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Grades_Curricula_CurriculumId",
                table: "Grades",
                column: "CurriculumId",
                principalTable: "Curricula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Grades_GradeId",
                table: "Subjects",
                column: "GradeId",
                principalTable: "Grades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
