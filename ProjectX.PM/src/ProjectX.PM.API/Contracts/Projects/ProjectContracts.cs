using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.API.Contracts.Projects;

public sealed record ProjectResponse(
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

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string Description,
    string OwnerName,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? TargetDate);

public sealed record UpdateProjectRequest(
    string Code,
    string Name,
    string Description,
    string OwnerName,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? TargetDate);
