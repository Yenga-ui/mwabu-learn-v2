using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableLessonGenerationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LessonGenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestJson = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonGenerationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonGenerationJobs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonGenerationJobs_LessonGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "LessonGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonGenerationJobs_LessonPlans_LessonPlanId_OrganisationId",
                        columns: x => new { x.LessonPlanId, x.OrganisationId },
                        principalTable: "LessonPlans",
                        principalColumns: new[] { "Id", "OrganisationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_ActorUserId",
                table: "LessonGenerationJobs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_GenerationId",
                table: "LessonGenerationJobs",
                column: "GenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_LessonPlanId_OrganisationId",
                table: "LessonGenerationJobs",
                columns: new[] { "LessonPlanId", "OrganisationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_OrganisationId_ActorUserId_CreatedAt",
                table: "LessonGenerationJobs",
                columns: new[] { "OrganisationId", "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_State",
                table: "LessonGenerationJobs",
                column: "State",
                unique: true,
                filter: "\"State\" = 'processing'");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGenerationJobs_State_CreatedAt",
                table: "LessonGenerationJobs",
                columns: new[] { "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonGenerationJobs");
        }
    }
}
