namespace ProjectX.IAM.Application.Authorization;

public static class BuiltInRoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PmPortfolioAdmin = "PM Portfolio Admin";
    public const string ProjectAdmin = "Project Admin";

    public static int GetDisplayOrder(string? roleName)
    {
        return roleName switch
        {
            SuperAdmin => 0,
            PmPortfolioAdmin => 1,
            ProjectAdmin => 2,
            _ => 100
        };
    }
}
