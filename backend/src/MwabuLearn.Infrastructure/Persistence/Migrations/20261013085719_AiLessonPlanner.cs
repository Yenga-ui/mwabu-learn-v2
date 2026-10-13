using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiLessonPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LessonPlannerSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAiApproved = table.Column<bool>(type: "boolean", nullable: false),
                    WebSearchApproved = table.Column<bool>(type: "boolean", nullable: false),
                    DailyRequestLimit = table.Column<int>(type: "integer", nullable: false),
                    MonthlyTokenLimit = table.Column<int>(type: "integer", nullable: false),
                    MonthlyCostLimitUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    SourceRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonPlannerSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonPlannerSettings_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    LearningOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DerivedFromId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsOrganisationTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonPlans", x => x.Id);
                    table.UniqueConstraint("AK_LessonPlans_Id_OrganisationId", x => new { x.Id, x.OrganisationId });
                    table.CheckConstraint("CK_LessonPlan_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_LessonPlans_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonPlans_LearningOutcomes_LearningOutcomeId",
                        column: x => x.LearningOutcomeId,
                        principalTable: "LearningOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonPlans_LessonPlans_DerivedFromId",
                        column: x => x.DerivedFromId,
                        principalTable: "LessonPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonPlans_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonUsagePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChargedTokens = table.Column<int>(type: "integer", nullable: false),
                    ChargedCostUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonUsagePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonUsagePeriods_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseRevision = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ReservedTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    UsageEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResultJson = table.Column<string>(type: "character varying(250000)", maxLength: 250000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonGenerations_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonGenerations_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonReviews_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonReviews_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DocumentJson = table.Column<string>(type: "character varying(250000)", maxLength: 250000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonRevisions_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonRevisions_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonShares_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonShares_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Licence = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublicationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetrievalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    EvidenceDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Credibility = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    IsExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ReusePermitted = table.Column<bool>(type: "boolean", nullable: false),
                    PermissionEvidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonSources_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonSources_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAt", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("52000000-0000-0000-0000-000000000014"), "lessons.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons read", null },
                    { new Guid("52000000-0000-0000-0000-000000000015"), "lessons.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000016"), "lessons.review", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons review", null },
                    { new Guid("52000000-0000-0000-0000-000000000017"), "lessons.publish", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons publish", null },
                    { new Guid("52000000-0000-0000-0000-000000000018"), "lessons.ai", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons ai", null },
                    { new Guid("52000000-0000-0000-0000-000000000019"), "lessons.configure", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "lessons configure", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedAt", "PermissionId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("53000000-0000-0000-0001-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000016"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000016"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000017"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000017"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000019"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000019"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0002-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000016"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000016"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000017"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000017"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000019"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000019"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0003-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0004-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000016"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000016"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000017"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000017"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0005-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0008-000000000014"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000014"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000015"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000015"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000016"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000016"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000017"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000017"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000018"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000018"), new Guid("51000000-0000-0000-0000-000000000008"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerations_ActorUserId",
                table: "LessonGenerations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerations_ExpiresAt",
                table: "LessonGenerations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerations_LessonPlanId_OrganisationId",
                table: "LessonGenerations",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerations_OrganisationId_CreatedAt",
                table: "LessonGenerations",
                columns: new[] { "OrganisationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlannerSettings_OrganisationId",
                table: "LessonPlannerSettings",
                column: "OrganisationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_DerivedFromId",
                table: "LessonPlans",
                column: "DerivedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_LearningOutcomeId",
                table: "LessonPlans",
                column: "LearningOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_OrganisationId_OwnerUserId_UpdatedAt",
                table: "LessonPlans",
                columns: new[] { "OrganisationId", "OwnerUserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_OrganisationId_Status_IsOrganisationTemplate",
                table: "LessonPlans",
                columns: new[] { "OrganisationId", "Status", "IsOrganisationTemplate" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_OwnerUserId",
                table: "LessonPlans",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonReviews_ActorUserId",
                table: "LessonReviews",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonReviews_LessonPlanId_CreatedAt",
                table: "LessonReviews",
                columns: new[] { "LessonPlanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonReviews_LessonPlanId_OrganisationId",
                table: "LessonReviews",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonRevisions_ActorUserId",
                table: "LessonRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonRevisions_LessonPlanId_Number",
                table: "LessonRevisions",
                columns: new[] { "LessonPlanId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonRevisions_LessonPlanId_OrganisationId",
                table: "LessonRevisions",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonShares_LessonPlanId_OrganisationId",
                table: "LessonShares",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonShares_LessonPlanId_UserId",
                table: "LessonShares",
                columns: new[] { "LessonPlanId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonShares_UserId",
                table: "LessonShares",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSources_ContentItemId",
                table: "LessonSources",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSources_ExpiresAt",
                table: "LessonSources",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSources_LessonPlanId_ContentItemId",
                table: "LessonSources",
                columns: new[] { "LessonPlanId", "ContentItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonSources_LessonPlanId_OrganisationId",
                table: "LessonSources",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSources_LessonPlanId_Url",
                table: "LessonSources",
                columns: new[] { "LessonPlanId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonUsagePeriods_OrganisationId_Month",
                table: "LessonUsagePeriods",
                columns: new[] { "OrganisationId", "Month" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION mwabu_lesson_revision_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Lesson revision snapshots are immutable' USING ERRCODE = '55000';
                END;
                $$;
                CREATE TRIGGER lesson_revision_immutable BEFORE UPDATE OR DELETE ON "LessonRevisions"
                FOR EACH ROW EXECUTE FUNCTION mwabu_lesson_revision_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS lesson_revision_immutable ON \"LessonRevisions\"; DROP FUNCTION IF EXISTS mwabu_lesson_revision_immutable();");
            migrationBuilder.DropTable(
                name: "LessonGenerations");

            migrationBuilder.DropTable(
                name: "LessonPlannerSettings");

            migrationBuilder.DropTable(
                name: "LessonReviews");

            migrationBuilder.DropTable(
                name: "LessonRevisions");

            migrationBuilder.DropTable(
                name: "LessonShares");

            migrationBuilder.DropTable(
                name: "LessonSources");

            migrationBuilder.DropTable(
                name: "LessonUsagePeriods");

            migrationBuilder.DropTable(
                name: "LessonPlans");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000016"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000017"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000018"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0001-000000000019"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000016"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000017"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000018"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0002-000000000019"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000018"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000016"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000017"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0004-000000000018"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0005-000000000018"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0008-000000000014"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0008-000000000015"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0008-000000000016"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0008-000000000017"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0008-000000000018"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("52000000-0000-0000-0000-000000000019"));
        }
    }
}
