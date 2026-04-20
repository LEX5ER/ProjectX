using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsolateRolesAndPermissionsPerProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleAssignments_Projects_ProjectId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Name",
                table: "Permissions");

            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ProjectId",
                table: "Roles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles",
                columns: new[] { "Scope", "Name" },
                unique: true,
                filter: "[ProjectId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_ProjectId_Name",
                table: "Roles",
                columns: new[] { "Scope", "ProjectId", "Name" },
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true,
                filter: "[ProjectId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ProjectId_Name",
                table: "Permissions",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Projects_ProjectId",
                table: "Permissions",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Projects_ProjectId",
                table: "Roles",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleAssignments_Projects_ProjectId",
                table: "UserRoleAssignments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Projects_ProjectId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Projects_ProjectId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleAssignments_Projects_ProjectId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Roles_ProjectId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_ProjectId_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Name",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ProjectId_Name",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles",
                columns: new[] { "Scope", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleAssignments_Projects_ProjectId",
                table: "UserRoleAssignments",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
