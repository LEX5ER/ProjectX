using Microsoft.EntityFrameworkCore;
using ProjectX.PM.Application.Abstractions;
using ProjectX.PM.Application.Auth;
using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Application.Projects;

public sealed class ProjectsService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IIamAuthorizationContextService authorizationContextService) : IProjectsService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<ProjectModel>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanReadAnyProject(accessContext);

        var query = dbContext.Projects
            .AsNoTracking()
            .AsQueryable();

        if (!accessContext.CanReadAllProjects && accessContext.ActivePmProjectId.HasValue)
        {
            query = query.Where(project => project.Id == accessContext.ActivePmProjectId.Value);
        }

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<ProjectModel>> GetProjectCatalogAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return CreatePagedResultAsync(
            dbContext.Projects.AsNoTracking(),
            page,
            pageSize,
            cancellationToken);
    }

    public async Task<ProjectModel?> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanReadProject(accessContext, id);

        return await dbContext.Projects
            .AsNoTracking()
            .Where(currentProject => currentProject.Id == id)
            .Select(currentProject => Map(currentProject))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectModel> CreateProjectAsync(
        string code,
        string name,
        string description,
        string ownerName,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);

        if (!accessContext.CanCreateProjects)
        {
            throw new ApplicationForbiddenException("Global ProjectX.PM admin access is required to create projects.");
        }

        ValidateProject(code, name, description, ownerName, startDate, targetDate);

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicateExists = await dbContext.Projects
            .AnyAsync(project => project.Code == normalizedCode, cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A project with the same code already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = name.Trim(),
            Description = description.Trim(),
            OwnerName = ownerName.Trim(),
            Status = status,
            StartDate = startDate,
            TargetDate = targetDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(project);
    }

    public async Task<ProjectModel?> UpdateProjectAsync(
        Guid id,
        string code,
        string name,
        string description,
        string ownerName,
        ProjectStatus status,
        DateOnly? startDate,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanWriteProject(accessContext, id);
        ValidateProject(code, name, description, ownerName, startDate, targetDate);

        var project = await dbContext.Projects
            .FirstOrDefaultAsync(currentProject => currentProject.Id == id, cancellationToken);

        if (project is null)
        {
            return null;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicateExists = await dbContext.Projects
            .AnyAsync(currentProject => currentProject.Id != id && currentProject.Code == normalizedCode, cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A project with the same code already exists.");
        }

        project.Code = normalizedCode;
        project.Name = name.Trim();
        project.Description = description.Trim();
        project.OwnerName = ownerName.Trim();
        project.Status = status;
        project.StartDate = startDate;
        project.TargetDate = targetDate;
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(project);
    }

    public async Task<bool> DeleteProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);

        if (!accessContext.CanCreateProjects)
        {
            throw new ApplicationForbiddenException("Global ProjectX.PM admin access is required to delete projects.");
        }

        var project = await dbContext.Projects
            .FirstOrDefaultAsync(currentProject => currentProject.Id == id, cancellationToken);

        if (project is null)
        {
            return false;
        }

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<PmAuthorizationContext> GetAuthorizationContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationContextService.GetCurrentAsync(requestContextAccessor.GetCurrent(), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            throw new ApplicationServiceUnavailableException(
                "IAM authorization unavailable",
                "ProjectX.PM could not validate the current IAM project.");
        }
    }

    private static void EnsureCanReadAnyProject(PmAuthorizationContext accessContext)
    {
        if (accessContext.CanReadAnyProject)
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeScopedAccessFailure(accessContext, "view"));
    }

    private static void EnsureCanReadProject(PmAuthorizationContext accessContext, Guid projectId)
    {
        if (accessContext.CanReadProject(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProjectFailure(accessContext, projectId, "view"));
    }

    private static void EnsureCanWriteProject(PmAuthorizationContext accessContext, Guid projectId)
    {
        if (accessContext.CanWriteProject(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProjectFailure(accessContext, projectId, "administer"));
    }

    private static void ValidateProject(
        string code,
        string name,
        string description,
        string ownerName,
        DateOnly? startDate,
        DateOnly? targetDate)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Code is required."];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            errors["ownerName"] = ["Owner name is required."];
        }

        if (startDate.HasValue && targetDate.HasValue && targetDate.Value < startDate.Value)
        {
            errors["targetDate"] = ["Target date cannot be earlier than the start date."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static string DescribeScopedAccessFailure(PmAuthorizationContext accessContext, string action)
    {
        if (!accessContext.ActivePmProjectId.HasValue)
        {
            return $"Select an IAM project with ProjectX.PM access before attempting to {action} projects.";
        }

        return $"This session does not have permission to {action} the active ProjectX.PM project.";
    }

    private static string DescribeProjectFailure(PmAuthorizationContext accessContext, Guid projectId, string action)
    {
        if (accessContext.CanCreateProjects && (action == "view" || action == "administer"))
        {
            return $"The requested project '{projectId}' is not available.";
        }

        if (accessContext.ActivePmProjectId == projectId)
        {
            return $"This session does not have permission to {action} the active ProjectX.PM project.";
        }

        if (accessContext.ActivePmProjectId.HasValue)
        {
            return $"This session can only {action} the ProjectX.PM project linked to the active IAM project.";
        }

        return DescribeScopedAccessFailure(accessContext, action);
    }

    private static ProjectModel Map(Project project)
    {
        return new ProjectModel(
            project.Id,
            project.Code,
            project.Name,
            project.Description,
            project.OwnerName,
            project.Status,
            project.StartDate,
            project.TargetDate,
            project.CreatedAtUtc,
            project.UpdatedAtUtc);
    }

    private static async Task<PagedResult<ProjectModel>> CreatePagedResultAsync(
        IQueryable<Project> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ThenBy(project => project.Name)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(project => Map(project))
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<ProjectModel>(items, normalizedPage, normalizedPageSize, totalCount, totalPages);
    }
}
