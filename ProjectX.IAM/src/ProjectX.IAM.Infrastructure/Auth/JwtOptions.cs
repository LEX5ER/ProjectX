namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string IdentityAudience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int IdentityTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;
}
