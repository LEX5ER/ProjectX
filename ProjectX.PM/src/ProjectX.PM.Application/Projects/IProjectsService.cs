using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Application.Projects;

public interface IProjectsService
{
    Task<PagedResult<ProjectModel>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<ProjectModel>> GetProjectCatalogAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ProjectModel?> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProjectModel> CreateProjectAsync(
        string code,
        string name,
        string description,
        string ownerName,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? targetDate,
        CancellationToken cancellationToken);

    Task<ProjectModel?> UpdateProjectAsync(
        Guid id,
        string code,
        string name,
        string description,
        string ownerName,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? targetDate,
        CancellationToken cancellationToken);

    Task<bool> DeleteProjectAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ProjectModel(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string OwnerName,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
