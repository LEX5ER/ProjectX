using ProjectX.IAM.Application.Authorization;

namespace ProjectX.IAM.Infrastructure.Auth;

internal static class ProjectUserSeedDefinitions
{
    private const string UserNamePrefix = "LEX1ER";
    private const string EmailDomain = "projectx.local";

    public static readonly ProjectUserSeedDefinition[] All =
    [
        new("A", BuiltInRoleNames.ProjectAdmin),
        new("R", "Readonly User")
    ];

    public static string BuildUserName(string projectCode, string suffix)
    {
        return $"{UserNamePrefix}-{NormalizeProjectCode(projectCode)}-{suffix.Trim().ToUpperInvariant()}";
    }

    public static string BuildEmail(string projectCode, string suffix)
    {
        return $"{UserNamePrefix.ToLowerInvariant()}-{NormalizeProjectCode(projectCode).ToLowerInvariant()}-{suffix.Trim().ToLowerInvariant()}@{EmailDomain}";
    }

    private static string NormalizeProjectCode(string projectCode)
    {
        return projectCode.Trim().ToUpperInvariant();
    }
}

internal sealed record ProjectUserSeedDefinition(
    string Suffix,
    string RoleName);
