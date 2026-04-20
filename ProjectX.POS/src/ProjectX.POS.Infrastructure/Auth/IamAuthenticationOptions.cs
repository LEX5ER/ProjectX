namespace ProjectX.POS.Infrastructure.Auth;

public sealed class IamAuthenticationOptions
{
    public const string SectionName = "IamAuth";

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string[] ValidAudiences { get; set; } = [];

    public string SigningKey { get; set; } = string.Empty;
}
