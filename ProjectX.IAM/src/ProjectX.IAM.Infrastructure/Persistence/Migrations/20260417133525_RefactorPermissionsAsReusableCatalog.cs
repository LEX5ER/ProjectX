using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPermissionsAsReusableCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Roles_RoleId",
                table: "Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Permissions",
                table: "Permissions");

            migrationBuilder.RenameTable(
                name: "Permissions",
                newName: "Permissions_PreCatalog");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [Permissions] ([Id], [Name], [Description])
                SELECT NEWID(), [source].[Name], MIN([source].[Description])
                FROM [Permissions_PreCatalog] AS [source]
                GROUP BY [source].[Name];
                """);

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT [source].[RoleId], [catalog].[Id]
                FROM [Permissions_PreCatalog] AS [source]
                INNER JOIN [Permissions] AS [catalog] ON [catalog].[Name] = [source].[Name];
                """);

            migrationBuilder.DropTable(
                name: "Permissions_PreCatalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions_PreCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions_PreCatalog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [Permissions_PreCatalog] ([Id], [Name], [Description], [RoleId])
                SELECT NEWID(), [catalog].[Name], [catalog].[Description], [assignments].[RoleId]
                FROM [RolePermissions] AS [assignments]
                INNER JOIN [Permissions] AS [catalog] ON [catalog].[Id] = [assignments].[PermissionId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_RoleId_Name",
                table: "Permissions_PreCatalog",
                columns: new[] { "RoleId", "Name" },
                unique: true);

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.RenameTable(
                name: "Permissions_PreCatalog",
                newName: "Permissions");

            migrationBuilder.Sql("""
                EXEC sp_rename N'[dbo].[PK_Permissions_PreCatalog]', N'PK_Permissions', N'OBJECT';
                """);
        }
    }
}
