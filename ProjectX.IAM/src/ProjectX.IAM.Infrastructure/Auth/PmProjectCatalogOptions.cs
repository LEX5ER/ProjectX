namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class PmProjectCatalogOptions
{
    public const string SectionName = "PmCatalog";

    public string ApiBaseUrl { get; set; } = string.Empty;
}
