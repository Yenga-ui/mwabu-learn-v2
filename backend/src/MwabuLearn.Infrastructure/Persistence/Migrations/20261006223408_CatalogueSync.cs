using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncChanges",
                columns: table => new
                {
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncChanges", x => new { x.Version, x.Ordinal });
                });

            migrationBuilder.CreateTable(
                name: "SyncCheckpoints",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCheckpoints", x => new { x.UserId, x.DeviceId, x.Scope });
                    table.ForeignKey(
                        name: "FK_SyncCheckpoints_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SyncCheckpoints_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncClock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncClock", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SyncClock",
                columns: new[] { "Id", "Version" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_SyncChanges_CreatedAt",
                table: "SyncChanges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncChanges_EntityType_EntityId_Version",
                table: "SyncChanges",
                columns: new[] { "EntityType", "EntityId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncCheckpoints_DeviceId",
                table: "SyncCheckpoints",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncChanges");

            migrationBuilder.DropTable(
                name: "SyncCheckpoints");

            migrationBuilder.DropTable(
                name: "SyncClock");
        }
    }
}
