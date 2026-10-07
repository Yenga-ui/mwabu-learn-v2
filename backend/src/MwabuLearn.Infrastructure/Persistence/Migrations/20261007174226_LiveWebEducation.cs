using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveWebEducation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrganisationMemberships_Id_OrganisationId",
                table: "OrganisationMemberships",
                columns: new[] { "Id", "OrganisationId" });

            migrationBuilder.CreateTable(
                name: "EducationProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurriculumVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationProjects", x => x.Id);
                    table.CheckConstraint("CK_Project_Dates", "\"EndsAt\" IS NULL OR \"StartsAt\" IS NULL OR \"EndsAt\" >= \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_EducationProjects_CurriculumVersions_CurriculumVersionId",
                        column: x => x.CurriculumVersionId,
                        principalTable: "CurriculumVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationProjects_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuardianLearners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardianMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearnerMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianLearners", x => x.Id);
                    table.CheckConstraint("CK_Guardian_NotSelf", "\"GuardianMembershipId\" <> \"LearnerMembershipId\"");
                    table.ForeignKey(
                        name: "FK_GuardianLearners_OrganisationMemberships_GuardianMembership~",
                        columns: x => new { x.GuardianMembershipId, x.OrganisationId },
                        principalTable: "OrganisationMemberships",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianLearners_OrganisationMemberships_LearnerMembershipI~",
                        columns: x => new { x.LearnerMembershipId, x.OrganisationId },
                        principalTable: "OrganisationMemberships",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianLearners_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationCurricula",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationCurricula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationCurricula_CurriculumVersions_CurriculumVersionId",
                        column: x => x.CurriculumVersionId,
                        principalTable: "CurriculumVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationCurricula_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastOpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceVisits_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceVisits_OrganisationMemberships_OrganisationMembersh~",
                        column: x => x.OrganisationMembershipId,
                        principalTable: "OrganisationMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_EducationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "EducationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_OrganisationMemberships_OrganisationMem~",
                        column: x => x.OrganisationMembershipId,
                        principalTable: "OrganisationMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectResources_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectResources_EducationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "EducationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSites_EducationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "EducationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSites_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAt", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("52000000-0000-0000-0000-000000000012"), "projects.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "projects read", null },
                    { new Guid("52000000-0000-0000-0000-000000000013"), "projects.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "projects manage", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedAt", "PermissionId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("53000000-0000-0000-0001-000000000012"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000012"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000013"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000013"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0002-000000000012"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000012"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000013"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000013"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0003-000000000012"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000012"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000013"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000013"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0004-000000000012"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000012"), new Guid("51000000-0000-0000-0000-000000000004"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EducationProjects_CurriculumVersionId",
                table: "EducationProjects",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationProjects_OrganisationId_Code",
                table: "EducationProjects",
                columns: new[] { "OrganisationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuardianLearners_GuardianMembershipId_LearnerMembershipId",
                table: "GuardianLearners",
                columns: new[] { "GuardianMembershipId", "LearnerMembershipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuardianLearners_GuardianMembershipId_OrganisationId",
                table: "GuardianLearners",
                columns: new[] { "GuardianMembershipId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianLearners_LearnerMembershipId_OrganisationId",
                table: "GuardianLearners",
                columns: new[] { "LearnerMembershipId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianLearners_OrganisationId",
                table: "GuardianLearners",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationCurricula_CurriculumVersionId",
                table: "OrganisationCurricula",
                column: "CurriculumVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationCurricula_OrganisationId_CurriculumVersionId",
                table: "OrganisationCurricula",
                columns: new[] { "OrganisationId", "CurriculumVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_OrganisationMembershipId",
                table: "ProjectParticipants",
                column: "OrganisationMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_ProjectId_OrganisationMembershipId",
                table: "ProjectParticipants",
                columns: new[] { "ProjectId", "OrganisationMembershipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_ContentItemId",
                table: "ProjectResources",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_ProjectId_ContentItemId",
                table: "ProjectResources",
                columns: new[] { "ProjectId", "ContentItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSites_OrganisationId",
                table: "ProjectSites",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSites_ProjectId_OrganisationId",
                table: "ProjectSites",
                columns: new[] { "ProjectId", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVisits_ContentItemId",
                table: "ResourceVisits",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVisits_OrganisationMembershipId_ContentItemId",
                table: "ResourceVisits",
                columns: new[] { "OrganisationMembershipId", "ContentItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceVisits_OrganisationMembershipId_LastOpenedAt",
                table: "ResourceVisits",
                columns: new[] { "OrganisationMembershipId", "LastOpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuardianLearners");

            migrationBuilder.DropTable(
                name: "OrganisationCurricula");

            migrationBuilder.DropTable(
                name: "ProjectParticipants");

            migrationBuilder.DropTable(
                name: "ProjectResources");

            migrationBuilder.DropTable(
                name: "ProjectSites");

            migrationBuilder.DropTable(
                name: "ResourceVisits");

            migrationBuilder.DropTable(
                name: "EducationProjects");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrganisationMemberships_Id_OrganisationId",
                table: "OrganisationMemberships");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000012"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000013"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000012"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000013"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000012"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000013"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000012"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000013"));
        }
    }
}
