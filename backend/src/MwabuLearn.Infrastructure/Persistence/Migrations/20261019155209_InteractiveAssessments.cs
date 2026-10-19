using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InteractiveAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_ContentAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "ContentAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    CorrectJson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentAttempts", x => x.Id);
                    table.CheckConstraint("CK_AssessmentAttempts_Score", "\"Score\" >= 0 AND \"Score\" <= \"Total\" AND \"Total\" BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_AssessmentAttempts_AspNetUsers_LearnerUserId",
                        column: x => x.LearnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentAttempts_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentAttempts_QuestionBanks_QuestionBankId",
                        column: x => x.QuestionBankId,
                        principalTable: "QuestionBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAt", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("52000000-0000-0000-0000-000000000020"), "assessments.take", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "assessments take", null },
                    { new Guid("52000000-0000-0000-0000-000000000021"), "assessments.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "assessments manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000022"), "assessments.report", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "assessments report", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedAt", "PermissionId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("53000000-0000-0000-0001-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000021"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000021"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000022"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000022"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0002-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000021"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000021"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000022"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000022"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0004-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000021"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000021"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000022"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000022"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0005-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000021"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000021"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000022"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000022"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0006-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000006"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_ExpiresAt",
                table: "AssessmentAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_LearnerUserId",
                table: "AssessmentAttempts",
                column: "LearnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_OrganisationId_LearnerUserId_SubmissionK~",
                table: "AssessmentAttempts",
                columns: new[] { "OrganisationId", "LearnerUserId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_QuestionBankId",
                table: "AssessmentAttempts",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_AssetId",
                table: "QuestionBanks",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_CreatedByUserId",
                table: "QuestionBanks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_OrganisationId_AssetId",
                table: "QuestionBanks",
                columns: new[] { "OrganisationId", "AssetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentAttempts");

            migrationBuilder.DropTable(
                name: "QuestionBanks");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000020"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000021"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000022"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000020"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000021"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000022"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000020"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000021"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000022"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000020"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000021"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000022"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0006-000000000020"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000022"));
        }
    }
}
