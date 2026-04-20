using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectScopeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Roles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Permissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Roles]
                SET [ProjectName] = [ProjectScopes].[DisplayName]
                FROM [Roles]
                INNER JOIN [ProjectScopes] ON [ProjectScopes].[Id] = [Roles].[ProjectScopeId]
                WHERE [Roles].[ProjectScopeId] IS NOT NULL;

                UPDATE [Permissions]
                SET [ProjectName] = [ProjectScopes].[DisplayName]
                FROM [Permissions]
                INNER JOIN [ProjectScopes] ON [ProjectScopes].[Id] = [Permissions].[ProjectScopeId]
                WHERE [Permissions].[ProjectScopeId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_ProjectScopes_ProjectScopeId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_ProjectScopes_ProjectScopeId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleAssignments_ProjectScopes_ProjectScopeId",
                table: "UserRoleAssignments");

            migrationBuilder.DropTable(
                name: "ProjectScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ProjectScopeId",
                table: "UserRoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Roles_ProjectScopeId",
                table: "Roles");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Permissions",
                newName: "ProjectScopeId");

            migrationBuilder.CreateTable(
                name: "ProjectScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalProjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectScopes", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [ProjectScopes] ([Id], [Description], [DisplayName], [ExternalProjectId], [SourceSystem])
                SELECT DISTINCT
                    [Projects].[ProjectId],
                    N'Generated during rollback from project-based access.',
                    COALESCE(NULLIF([Projects].[ProjectName], N''), CONVERT(nvarchar(100), [Projects].[ProjectId])),
                    CONVERT(nvarchar(100), [Projects].[ProjectId]),
                    N'Rollback'
                FROM
                (
                    SELECT [ProjectScopeId] AS [ProjectId], [ProjectName]
                    FROM [Roles]
                    WHERE [ProjectScopeId] IS NOT NULL

                    UNION

                    SELECT [ProjectScopeId] AS [ProjectId], [ProjectName]
                    FROM [Permissions]
                    WHERE [ProjectScopeId] IS NOT NULL
                ) AS [Projects];
                """);

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
                name: "IX_Roles_ProjectScopeId",
                table: "Roles",
                column: "ProjectScopeId");

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

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Permissions");

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
    }
}
