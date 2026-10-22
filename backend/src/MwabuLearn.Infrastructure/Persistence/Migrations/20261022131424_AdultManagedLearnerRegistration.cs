using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MwabuLearn.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdultManagedLearnerRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredByUserId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationOrganisationId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RegisteredByUserId",
                table: "AspNetUsers",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RegistrationOrganisationId",
                table: "AspNetUsers",
                column: "RegistrationOrganisationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_RegisteredByUserId",
                table: "AspNetUsers",
                column: "RegisteredByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organisations_RegistrationOrganisationId",
                table: "AspNetUsers",
                column: "RegistrationOrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_RegisteredByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Organisations_RegistrationOrganisationId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RegisteredByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RegistrationOrganisationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegisteredByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationOrganisationId",
                table: "AspNetUsers");
        }
    }
}
