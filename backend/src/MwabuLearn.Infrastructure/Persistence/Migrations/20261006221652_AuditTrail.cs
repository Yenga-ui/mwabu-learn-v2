using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventType_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt_Id",
                table: "AuditEvents",
                columns: new[] { "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganisationId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "OrganisationId", "OccurredAt" });
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Audit records are append-only'; END; $$;
                CREATE TRIGGER audit_events_append_only BEFORE UPDATE OR DELETE ON "AuditEvents"
                FOR EACH ROW EXECUTE FUNCTION reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION reject_audit_mutation() CASCADE;");
            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
