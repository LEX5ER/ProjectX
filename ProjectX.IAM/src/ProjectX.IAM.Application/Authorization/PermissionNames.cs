namespace ProjectX.IAM.Application.Authorization;

public static class PermissionNames
{
    public const string DashboardRead = "dashboard.read";
    public const string ProjectsRead = "projects.read";
    public const string ProjectsWrite = "projects.write";
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string PermissionsRead = "permissions.read";
    public const string PermissionsWrite = "permissions.write";
    public const string ReportsRead = "reports.read";
    public const string TokensManage = "tokens.manage";

    public static readonly string[] BuiltIn =
    [
        DashboardRead,
        ProjectsRead,
        ProjectsWrite,
        UsersRead,
        UsersWrite,
        RolesRead,
        RolesWrite,
        PermissionsRead,
        PermissionsWrite,
        ReportsRead,
        TokensManage
    ];
}
