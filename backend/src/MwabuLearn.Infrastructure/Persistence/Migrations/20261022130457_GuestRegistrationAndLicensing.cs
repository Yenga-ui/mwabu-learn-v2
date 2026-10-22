using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestRegistrationAndLicensing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GuestCanUse",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GuestLearnerVisible",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GuestTeacherVisible",
                table: "ContentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedPolicyId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLicensed",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenceUpdatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LicenceUpdatedBy",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PolicyAcceptedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationRole",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // Explicitly authorised transition: retain existing account entitlements.
            // New account inserts retain the false default and require administrator activation.
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"IsLicensed\" = TRUE, \"LicenceUpdatedAt\" = CURRENT_TIMESTAMP");

            migrationBuilder.CreateTable(
                name: "RegistrationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "character varying(30000)", maxLength: 30000, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AllowLearnerRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationPolicies_AspNetUsers_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AcceptedPolicyId",
                table: "AspNetUsers",
                column: "AcceptedPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPolicies_PublishedBy",
                table: "RegistrationPolicies",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPolicies_Version",
                table: "RegistrationPolicies",
                column: "Version",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_RegistrationPolicies_AcceptedPolicyId",
                table: "AspNetUsers",
                column: "AcceptedPolicyId",
                principalTable: "RegistrationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_RegistrationPolicies_AcceptedPolicyId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "RegistrationPolicies");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AcceptedPolicyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GuestCanUse",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "GuestLearnerVisible",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "GuestTeacherVisible",
                table: "ContentItems");

            migrationBuilder.DropColumn(
                name: "AcceptedPolicyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsLicensed",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenceUpdatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenceUpdatedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PolicyAcceptedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationRole",
                table: "AspNetUsers");
        }
    }
}
