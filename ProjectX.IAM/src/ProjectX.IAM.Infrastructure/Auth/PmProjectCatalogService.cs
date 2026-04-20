using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Domain.Entities;
using ProjectX.IAM.Infrastructure.Persistence;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class PmProjectCatalogService(
    HttpClient httpClient,
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedIdentityOptions> seedIdentityOptions) : IPmProjectCatalogService
{
    private const int CodeMaxLength = 50;
    private const int DisplayNameMaxLength = 100;
    private const int DescriptionMaxLength = 256;
    private const int CatalogPageSize = 100;

    public async Task<IReadOnlyList<PmProjectCatalogProject>> GetAllProjectsAsync(CancellationToken cancellationToken)
    {
        var responses = await FetchAllProjectsAsync(
            "/api/projects/catalog",
            configureRequest: null,
            cancellationToken);
        var projects = MapProjects(responses);

        await EnsureProjectArtifactsAsync(projects, cancellationToken);

        return projects;
    }

    public async Task<IReadOnlyList<PmProjectCatalogProject>> GetAccessibleProjectsAsync(
        string authorizationHeader,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            throw new InvalidOperationException("Authorization header is missing.");
        }

        var responses = await FetchAllProjectsAsync(
            "/api/projects",
            request =>
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);

                if (projectId.HasValue)
                {
                    request.Headers.TryAddWithoutValidation(ProjectContextHeaderNames.ProjectId, projectId.Value.ToString());
                }
            },
            cancellationToken);
        var projects = MapProjects(responses);

        await EnsureProjectArtifactsAsync(projects, cancellationToken);

        return projects;
    }

    private async Task<IReadOnlyList<PmProjectResponse>> FetchAllProjectsAsync(
        string endpointPath,
        Action<HttpRequestMessage>? configureRequest,
        CancellationToken cancellationToken)
    {
        var page = 1;
        var projects = new List<PmProjectResponse>();

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpointPath}?page={page}&pageSize={CatalogPageSize}");
            configureRequest?.Invoke(request);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"PM project catalog lookup failed with status code {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<PmPagedResponse>(cancellationToken: cancellationToken)
                ?? new PmPagedResponse([], page, CatalogPageSize, 0, 0);

            projects.AddRange(payload.Items);

            if (payload.TotalPages <= 0 || page >= payload.TotalPages)
            {
                break;
            }

            page++;
        }

        return projects;
    }

    private static PmProjectCatalogProject[] MapProjects(IReadOnlyList<PmProjectResponse> responses)
    {
        return responses
            .Select(project => new PmProjectCatalogProject(
                project.Id,
                Clip(string.IsNullOrWhiteSpace(project.Code) ? project.Name : project.Code, CodeMaxLength),
                Clip(string.IsNullOrWhiteSpace(project.Name) ? project.Code : project.Name, DisplayNameMaxLength),
                Clip(BuildDescription(project), DescriptionMaxLength)))
            .ToArray();
    }

    private async Task EnsureProjectArtifactsAsync(
        IReadOnlyList<PmProjectCatalogProject> projects,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0)
        {
            return;
        }

        var projectIds = projects.Select(project => project.Id).ToArray();
        var existingPermissions = await dbContext.Permissions
            .Where(permission => permission.ProjectId.HasValue && projectIds.Contains(permission.ProjectId.Value))
            .ToListAsync(cancellationToken);
        var existingPermissionsByKey = existingPermissions
            .ToDictionary(
                permission => $"{permission.ProjectId!.Value:N}:{permission.Name}",
                StringComparer.OrdinalIgnoreCase);
        var existingRoles = await dbContext.Roles
            .Include(role => role.Permissions)
            .Where(role =>
                role.Scope == RoleScope.Project
                && role.ProjectId.HasValue
                && projectIds.Contains(role.ProjectId.Value))
            .ToListAsync(cancellationToken);
        var existingRolesByKey = existingRoles
            .ToDictionary(
                role => BuildRoleKey(role.ProjectId!.Value, role.Name),
                StringComparer.OrdinalIgnoreCase);
        var hasChanges = false;

        foreach (var project in projects)
        {
            foreach (var permission in existingPermissions.Where(permission => permission.ProjectId == project.Id))
            {
                if (!string.Equals(permission.ProjectName, project.Name, StringComparison.Ordinal))
                {
                    permission.ProjectName = project.Name;
                    hasChanges = true;
                }
            }

            foreach (var role in existingRoles.Where(role => role.ProjectId == project.Id))
            {
                if (!string.Equals(role.ProjectName, project.Name, StringComparison.Ordinal))
                {
                    role.ProjectName = project.Name;
                    hasChanges = true;
                }
            }

            foreach (var permissionName in PermissionNames.BuiltIn)
            {
                var permissionKey = $"{project.Id:N}:{permissionName}";

                if (existingPermissionsByKey.ContainsKey(permissionKey))
                {
                    continue;
                }

                var permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = permissionName,
                    Description = Clip(
                        $"Allows {permissionName.Replace('.', ' ')} within {project.Name}.",
                        DescriptionMaxLength),
                    ProjectId = project.Id,
                    ProjectName = project.Name
                };

                dbContext.Permissions.Add(permission);

                existingPermissionsByKey[permissionKey] = permission;
                hasChanges = true;
            }

            hasChanges |= EnsureProjectRole(
                project,
                BuiltInRoleNames.ProjectAdmin,
                $"Protected top-level access role for the project {project.Name}.",
                isProtected: true,
                hasAllPermissions: true,
                permissionNames: [],
                existingPermissionsByKey,
                existingRolesByKey);

            foreach (var preset in RoleSeedPresets.All.Where(preset => preset.Scope == RoleScope.Project))
            {
                hasChanges |= EnsureProjectRole(
                    project,
                    preset.Name,
                    preset.Description,
                    isProtected: false,
                    preset.HasAllPermissions,
                    preset.PermissionNames,
                    existingPermissionsByKey,
                    existingRolesByKey);
            }
        }

        hasChanges |= await EnsureSeededUsersAsync(projects, existingRolesByKey, cancellationToken);

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private bool EnsureProjectRole(
        PmProjectCatalogProject project,
        string roleName,
        string description,
        bool isProtected,
        bool hasAllPermissions,
        string[] permissionNames,
        IDictionary<string, Permission> existingPermissionsByKey,
        IDictionary<string, Role> existingRolesByKey)
    {
        var roleKey = BuildRoleKey(project.Id, roleName);

        if (!existingRolesByKey.TryGetValue(roleKey, out var role))
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName
            };

            dbContext.Roles.Add(role);
            existingRolesByKey[roleKey] = role;
        }

        var hasChanges = false;
        var normalizedDescription = Clip(description, DescriptionMaxLength);

        if (!string.Equals(role.Name, roleName, StringComparison.Ordinal))
        {
            role.Name = roleName;
            hasChanges = true;
        }

        if (!string.Equals(role.Description, normalizedDescription, StringComparison.Ordinal))
        {
            role.Description = normalizedDescription;
            hasChanges = true;
        }

        if (role.Scope != RoleScope.Project)
        {
            role.Scope = RoleScope.Project;
            hasChanges = true;
        }

        if (role.ProjectId != project.Id)
        {
            role.ProjectId = project.Id;
            hasChanges = true;
        }

        if (!string.Equals(role.ProjectName, project.Name, StringComparison.Ordinal))
        {
            role.ProjectName = project.Name;
            hasChanges = true;
        }

        if (role.IsProtected != isProtected)
        {
            role.IsProtected = isProtected;
            hasChanges = true;
        }

        if (role.HasAllPermissions != hasAllPermissions)
        {
            role.HasAllPermissions = hasAllPermissions;
            hasChanges = true;
        }

        var expectedPermissions = permissionNames
            .Select(permissionName => existingPermissionsByKey[BuildPermissionKey(project.Id, permissionName)])
            .ToArray();

        foreach (var permission in role.Permissions.Where(permission => !expectedPermissions.Contains(permission)).ToArray())
        {
            role.Permissions.Remove(permission);
            hasChanges = true;
        }

        foreach (var permission in expectedPermissions.Where(permission => !role.Permissions.Contains(permission)))
        {
            role.Permissions.Add(permission);
            hasChanges = true;
        }

        return hasChanges;
    }

    private async Task<bool> EnsureSeededUsersAsync(
        IReadOnlyList<PmProjectCatalogProject> projects,
        IReadOnlyDictionary<string, Role> existingRolesByKey,
        CancellationToken cancellationToken)
    {
        var projectUserSeeds = projects
            .SelectMany(project => ProjectUserSeedDefinitions.All.Select(seed => new SeededProjectUser(
                project.Id,
                ProjectUserSeedDefinitions.BuildUserName(project.Code, seed.Suffix),
                ProjectUserSeedDefinitions.BuildEmail(project.Code, seed.Suffix),
                seed.RoleName)))
            .ToArray();

        if (projectUserSeeds.Length == 0)
        {
            return false;
        }

        var userNames = projectUserSeeds
            .Select(seed => seed.UserName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var emails = projectUserSeeds
            .Select(seed => seed.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingUsers = await dbContext.Users
            .Where(user => userNames.Contains(user.UserName) || emails.Contains(user.Email))
            .ToListAsync(cancellationToken);
        var usersByUserName = existingUsers
            .ToDictionary(user => user.UserName, StringComparer.OrdinalIgnoreCase);
        var usersByEmail = existingUsers
            .ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
        var existingUserIds = existingUsers
            .Select(user => user.Id)
            .ToArray();
        var projectIds = projects
            .Select(project => project.Id)
            .ToArray();
        var existingAssignments = await dbContext.UserRoleAssignments
            .Where(assignment =>
                assignment.ProjectId.HasValue
                && projectIds.Contains(assignment.ProjectId.Value)
                && existingUserIds.Contains(assignment.UserId))
            .ToListAsync(cancellationToken);
        var assignmentKeys = existingAssignments
            .Select(assignment => BuildAssignmentKey(assignment.UserId, assignment.RoleId, assignment.ProjectId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defaultPassword = string.IsNullOrWhiteSpace(seedIdentityOptions.Value.Password)
            ? "ChangeMe123!"
            : seedIdentityOptions.Value.Password;
        var hasChanges = false;

        foreach (var seed in projectUserSeeds)
        {
            var user = FindUser(seed, usersByUserName, usersByEmail);

            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    IsProtected = false,
                    UserName = seed.UserName,
                    Email = seed.Email
                };
                user.PasswordHash = passwordHasher.HashPassword(user, defaultPassword);
                dbContext.Users.Add(user);
                usersByUserName[user.UserName] = user;
                usersByEmail[user.Email] = user;
                hasChanges = true;
            }
            else
            {
                if (!string.Equals(user.UserName, seed.UserName, StringComparison.Ordinal))
                {
                    usersByUserName.Remove(user.UserName);
                    user.UserName = seed.UserName;
                    usersByUserName[user.UserName] = user;
                    hasChanges = true;
                }

                if (!string.Equals(user.Email, seed.Email, StringComparison.OrdinalIgnoreCase))
                {
                    usersByEmail.Remove(user.Email);
                    user.Email = seed.Email;
                    usersByEmail[user.Email] = user;
                    hasChanges = true;
                }

                if (user.IsProtected)
                {
                    user.IsProtected = false;
                    hasChanges = true;
                }
            }

            var role = existingRolesByKey[BuildRoleKey(seed.ProjectId, seed.RoleName)];
            var assignmentKey = BuildAssignmentKey(user.Id, role.Id, seed.ProjectId);

            if (assignmentKeys.Contains(assignmentKey))
            {
                continue;
            }

            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id,
                ProjectId = seed.ProjectId
            });

            assignmentKeys.Add(assignmentKey);
            hasChanges = true;
        }

        return hasChanges;
    }

    private static User? FindUser(
        SeededProjectUser seed,
        IReadOnlyDictionary<string, User> usersByUserName,
        IReadOnlyDictionary<string, User> usersByEmail)
    {
        if (usersByUserName.TryGetValue(seed.UserName, out var user))
        {
            return user;
        }

        return usersByEmail.TryGetValue(seed.Email, out user)
            ? user
            : null;
    }

    private static string BuildDescription(PmProjectResponse project)
    {
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            return project.Description.Trim();
        }

        return $"{project.Code.Trim()} project owned by {project.OwnerName.Trim()}.";
    }

    private static string Clip(string value, int maxLength)
    {
        var trimmed = value.Trim();

        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }

    private static string BuildPermissionKey(Guid projectId, string permissionName)
    {
        return $"{projectId:N}:{permissionName}";
    }

    private static string BuildRoleKey(Guid projectId, string roleName)
    {
        return $"{projectId:N}:{roleName}";
    }

    private static string BuildAssignmentKey(Guid userId, Guid roleId, Guid? projectId)
    {
        return $"{userId:N}:{roleId:N}:{(projectId.HasValue ? projectId.Value.ToString("N") : string.Empty)}";
    }

    private sealed record PmProjectResponse(
        Guid Id,
        string Code,
        string Name,
        string Description,
        string OwnerName);

    private sealed record PmPagedResponse(
        PmProjectResponse[] Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record SeededProjectUser(
        Guid ProjectId,
        string UserName,
        string Email,
        string RoleName);
}
