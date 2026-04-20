namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class SeedIdentityOptions
{
    public const string SectionName = "SeedIdentity";

    public string UserName { get; set; } = "admin";

    public string Email { get; set; } = "admin@projectx.local";

    public string Password { get; set; } = "ChangeMe123!";
}
