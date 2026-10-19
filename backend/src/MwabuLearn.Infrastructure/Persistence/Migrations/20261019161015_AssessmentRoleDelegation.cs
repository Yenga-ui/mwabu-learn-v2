using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentRoleDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedAt", "PermissionId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("53000000-0000-0000-0003-000000000020"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000020"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000021"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000021"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000022"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000022"), new Guid("51000000-0000-0000-0000-000000000003"), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000020"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000021"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("53000000-0000-0000-0003-000000000022"));
        }
    }
}
