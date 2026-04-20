using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Auth;

public static class RoleSeedPresets
{
    public static readonly RoleSeedPreset[] All =
    [
        new(
            BuiltInRoleNames.SuperAdmin,
            "Global access to every project, role, permission, and protected administrative operation.",
            RoleScope.Global,
            true,
            []),
        new(
            BuiltInRoleNames.PmPortfolioAdmin,
            "Global ProjectX.PM portfolio administration without full IAM super admin access.",
            RoleScope.Global,
            false,
            [
                PermissionNames.ProjectsRead,
                PermissionNames.ProjectsWrite
            ]),
        new(
            "Application Admin",
            "Full access inside a specific project.",
            RoleScope.Project,
            true,
            []),
        new(
            "Application User",
            "Interactive access to the application dashboard for a specific project.",
            RoleScope.Project,
            false,
            [PermissionNames.DashboardRead]),
        new(
            "Readonly User",
            "Read-only access across standard management surfaces for a specific project.",
            RoleScope.Project,
            false,
            [
                PermissionNames.DashboardRead,
                PermissionNames.ProjectsRead,
                PermissionNames.UsersRead,
                PermissionNames.RolesRead,
                PermissionNames.PermissionsRead,
                PermissionNames.ReportsRead
            ]),
        new(
            "Shareholder User",
            "Read-only access to reporting-oriented project views.",
            RoleScope.Project,
            false,
            [
                PermissionNames.DashboardRead,
                PermissionNames.ReportsRead
            ]),
        new(
            "Project Auditor",
            "Read-only access to project users, roles, permissions, and reports.",
            RoleScope.Project,
            false,
            [
                PermissionNames.DashboardRead,
                PermissionNames.UsersRead,
                PermissionNames.RolesRead,
                PermissionNames.PermissionsRead,
                PermissionNames.ReportsRead
            ]),
        new(
            "Support User",
            "Operational read access to project user and role assignments.",
            RoleScope.Project,
            false,
            [
                PermissionNames.DashboardRead,
                PermissionNames.UsersRead,
                PermissionNames.RolesRead
            ])
    ];
}
