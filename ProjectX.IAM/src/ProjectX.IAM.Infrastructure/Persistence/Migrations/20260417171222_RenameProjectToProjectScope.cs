using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectToProjectScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "IX_UserRoleAssignments_UserId_RoleId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ProjectId",
                table: "UserRoleAssignments");

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

            migrationBuilder.DropIndex(
                name: "IX_Projects_Name",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "UserRoleAssignments",
                newName: "ProjectScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoleAssignments_ProjectId_UserId",
                table: "UserRoleAssignments",
                newName: "IX_UserRoleAssignments_ProjectScopeId_UserId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Roles",
                newName: "ProjectScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_ProjectId",
                table: "Roles",
                newName: "IX_Roles_ProjectScopeId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Permissions",
                newName: "ProjectScopeId");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Projects",
                table: "Projects");

            migrationBuilder.RenameTable(
                name: "Projects",
                newName: "ProjectScopes");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "ProjectScopes",
                newName: "DisplayName");

            migrationBuilder.AddColumn<string>(
                name: "ExternalProjectId",
                table: "ProjectScopes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                table: "ProjectScopes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [ProjectScopes]
                SET [SourceSystem] = 'ProjectX.PM',
                    [ExternalProjectId] = CONVERT(nvarchar(100), [Id])
                WHERE [SourceSystem] IS NULL
                   OR [ExternalProjectId] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalProjectId",
                table: "ProjectScopes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceSystem",
                table: "ProjectScopes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectScopes",
                table: "ProjectScopes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId" },
                unique: true,
                filter: "[ProjectScopeId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ProjectScopeId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId", "ProjectScopeId" },
                unique: true,
                filter: "[ProjectScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles",
                columns: new[] { "Scope", "Name" },
                unique: true,
                filter: "[ProjectScopeId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_ProjectScopeId_Name",
                table: "Roles",
                columns: new[] { "Scope", "ProjectScopeId", "Name" },
                unique: true,
                filter: "[ProjectScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true,
                filter: "[ProjectScopeId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ProjectScopeId_Name",
                table: "Permissions",
                columns: new[] { "ProjectScopeId", "Name" },
                unique: true,
                filter: "[ProjectScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScopes_DisplayName",
                table: "ProjectScopes",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScopes_SourceSystem_ExternalProjectId",
                table: "ProjectScopes",
                columns: new[] { "SourceSystem", "ExternalProjectId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_ProjectScopes_ProjectScopeId",
                table: "Permissions",
                column: "ProjectScopeId",
                principalTable: "ProjectScopes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_ProjectScopes_ProjectScopeId",
                table: "Roles",
                column: "ProjectScopeId",
                principalTable: "ProjectScopes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleAssignments_ProjectScopes_ProjectScopeId",
                table: "UserRoleAssignments",
                column: "ProjectScopeId",
                principalTable: "ProjectScopes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_ProjectScopes_ProjectScopeId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_ProjectScopes_ProjectScopeId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleAssignments_ProjectScopes_ProjectScopeId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ProjectScopeId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Scope_ProjectScopeId_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Name",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ProjectScopeId_Name",
                table: "Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectScopes",
                table: "ProjectScopes");

            migrationBuilder.DropIndex(
                name: "IX_ProjectScopes_DisplayName",
                table: "ProjectScopes");

            migrationBuilder.DropIndex(
                name: "IX_ProjectScopes_SourceSystem_ExternalProjectId",
                table: "ProjectScopes");

            migrationBuilder.DropColumn(
                name: "ExternalProjectId",
                table: "ProjectScopes");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "ProjectScopes");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "ProjectScopes",
                newName: "Name");

            migrationBuilder.RenameTable(
                name: "ProjectScopes",
                newName: "Projects");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Projects",
                table: "Projects",
                column: "Id");

            migrationBuilder.RenameColumn(
                name: "ProjectScopeId",
                table: "UserRoleAssignments",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoleAssignments_ProjectScopeId_UserId",
                table: "UserRoleAssignments",
                newName: "IX_UserRoleAssignments_ProjectId_UserId");

            migrationBuilder.RenameColumn(
                name: "ProjectScopeId",
                table: "Roles",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_ProjectScopeId",
                table: "Roles",
                newName: "IX_Roles_ProjectId");

            migrationBuilder.RenameColumn(
                name: "ProjectScopeId",
                table: "Permissions",
                newName: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId" },
                unique: true,
                filter: "[ProjectId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ProjectId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId", "ProjectId" },
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

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
    }
}
