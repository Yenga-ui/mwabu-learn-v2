using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityOrganisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessTokenVersion = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrantsPlatformAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrganisationType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ParentOrganisationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                    table.CheckConstraint("CK_Organisations_NotSelfParent", "\"ParentOrganisationId\" IS NULL OR \"ParentOrganisationId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_Organisations_Organisations_ParentOrganisationId",
                        column: x => x.ParentOrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationMemberships_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_OrganisationRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrganisationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationMembershipRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationMembershipRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationMembershipRoles_OrganisationMemberships_Organis~",
                        column: x => x.OrganisationMembershipId,
                        principalTable: "OrganisationMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationMembershipRoles_OrganisationRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrganisationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OrganisationRoles",
                columns: new[] { "Id", "Code", "CreatedAt", "GrantsPlatformAuthority", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51000000-0000-0000-0000-000000000001"), "PlatformAdmin", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), true, "Platform Administrator", null },
                    { new Guid("51000000-0000-0000-0000-000000000002"), "OrganisationAdmin", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Organisation Administrator", null },
                    { new Guid("51000000-0000-0000-0000-000000000003"), "ProjectManager", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Project Manager", null },
                    { new Guid("51000000-0000-0000-0000-000000000004"), "HeadTeacher", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Head Teacher", null },
                    { new Guid("51000000-0000-0000-0000-000000000005"), "Teacher", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Teacher", null },
                    { new Guid("51000000-0000-0000-0000-000000000006"), "Learner", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Learner", null },
                    { new Guid("51000000-0000-0000-0000-000000000007"), "ParentGuardian", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Parent / Guardian", null },
                    { new Guid("51000000-0000-0000-0000-000000000008"), "ContentManager", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Content Manager", null },
                    { new Guid("51000000-0000-0000-0000-000000000009"), "DataAnalyst", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Data Analyst", null }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAt", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("52000000-0000-0000-0000-000000000001"), "curriculum.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "curriculum read", null },
                    { new Guid("52000000-0000-0000-0000-000000000002"), "curriculum.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "curriculum manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000003"), "content.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "content read", null },
                    { new Guid("52000000-0000-0000-0000-000000000004"), "content.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "content manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000005"), "content.publish", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "content publish", null },
                    { new Guid("52000000-0000-0000-0000-000000000006"), "users.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "users read", null },
                    { new Guid("52000000-0000-0000-0000-000000000007"), "users.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "users manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000008"), "organisations.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "organisations read", null },
                    { new Guid("52000000-0000-0000-0000-000000000009"), "organisations.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "organisations manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000010"), "memberships.manage", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "memberships manage", null },
                    { new Guid("52000000-0000-0000-0000-000000000011"), "reports.read", new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), "reports read", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedAt", "PermissionId", "RoleId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("53000000-0000-0000-0001-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000002"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000002"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000004"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000004"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000005"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000005"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000006"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000006"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000007"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000007"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000009"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000009"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000010"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000010"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0001-000000000011"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000011"), new Guid("51000000-0000-0000-0000-000000000001"), null },
                    { new Guid("53000000-0000-0000-0002-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000002"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000002"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000004"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000004"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000005"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000005"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000006"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000006"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000007"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000007"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000009"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000009"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000010"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000010"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0002-000000000011"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000011"), new Guid("51000000-0000-0000-0000-000000000002"), null },
                    { new Guid("53000000-0000-0000-0003-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000006"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000006"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000010"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000010"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0003-000000000011"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000011"), new Guid("51000000-0000-0000-0000-000000000003"), null },
                    { new Guid("53000000-0000-0000-0004-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000006"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000006"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0004-000000000011"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000011"), new Guid("51000000-0000-0000-0000-000000000004"), null },
                    { new Guid("53000000-0000-0000-0005-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0005-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000005"), null },
                    { new Guid("53000000-0000-0000-0006-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000006"), null },
                    { new Guid("53000000-0000-0000-0006-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000006"), null },
                    { new Guid("53000000-0000-0000-0007-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000007"), null },
                    { new Guid("53000000-0000-0000-0008-000000000001"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000001"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000003"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000003"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000004"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000004"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0008-000000000005"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000005"), new Guid("51000000-0000-0000-0000-000000000008"), null },
                    { new Guid("53000000-0000-0000-0009-000000000008"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000008"), new Guid("51000000-0000-0000-0000-000000000009"), null },
                    { new Guid("53000000-0000-0000-0009-000000000011"), new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("52000000-0000-0000-0000-000000000011"), new Guid("51000000-0000-0000-0000-000000000009"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive_Id",
                table: "AspNetUsers",
                columns: new[] { "IsActive", "Id" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMembershipRoles_OrganisationMembershipId_RoleId",
                table: "OrganisationMembershipRoles",
                columns: new[] { "OrganisationMembershipId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMembershipRoles_RoleId",
                table: "OrganisationMembershipRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMemberships_OrganisationId_IsActive",
                table: "OrganisationMemberships",
                columns: new[] { "OrganisationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMemberships_UserId_OrganisationId",
                table: "OrganisationMemberships",
                columns: new[] { "UserId", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRoles_Code",
                table: "OrganisationRoles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_Code",
                table: "Organisations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_IsActive_Name",
                table: "Organisations",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_ParentOrganisationId",
                table: "Organisations",
                column: "ParentOrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "OrganisationMembershipRoles");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "OrganisationMemberships");

            migrationBuilder.DropTable(
                name: "OrganisationRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Organisations");
        }
    }
}
