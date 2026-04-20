namespace ProjectX.IAM.Application.Management;

public sealed record PmProjectCatalogProject(
    Guid Id,
    string Code,
    string Name,
    string Description);

public interface IPmProjectCatalogService
{
    Task<IReadOnlyList<PmProjectCatalogProject>> GetAllProjectsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PmProjectCatalogProject>> GetAccessibleProjectsAsync(
        string authorizationHeader,
        Guid? projectId,
        CancellationToken cancellationToken);
}
