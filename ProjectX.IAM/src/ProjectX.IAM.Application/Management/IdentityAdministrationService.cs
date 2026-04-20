using Microsoft.EntityFrameworkCore;
using ProjectX.IAM.Application.Abstractions;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Application.Management;

public sealed class IdentityAdministrationService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IUserPasswordService userPasswordService,
    IPmProjectCatalogService pmProjectCatalogService) : IIdentityAdministrationService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<UserModel>> GetUsersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = NormalizePagination(page, pageSize);
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalUsersRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.UsersRead, cancellationToken);

        if (!hasGlobalUsersRead && requestContext.ProjectId is null)
        {
            throw CreateValidationException("projectId", "Select a project before listing project-scoped users.");
        }

        var query = LoadUserQuery().AsNoTracking();

        if (!(hasGlobalUsersRead && requestContext.ProjectId is null))
        {
            query = query.Where(user =>
                (requestContext.ProjectId.HasValue && user.RoleAssignments.Any(assignment =>
                    assignment.ProjectId == requestContext.ProjectId.Value
                    && assignment.Role.Scope == RoleScope.Project
                    && assignment.Role.ProjectId == requestContext.ProjectId.Value))
                || (hasGlobalUsersRead && user.RoleAssignments.Any(assignment =>
                    assignment.ProjectId == null
                    && assignment.Role.Scope == RoleScope.Global
                    && assignment.Role.ProjectId == null)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.UserName)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = users
            .Select(user => MapUser(user, requestContext.ProjectId, includeGlobalAssignments: hasGlobalUsersRead))
            .ToArray();

        return CreatePagedResult(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<UserModel?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalUsersRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.UsersRead, cancellationToken);
        var user = await LoadUserQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!CanAccessUser(user, requestContext.ProjectId, hasGlobalUsersRead))
        {
            if (requestContext.ProjectId is null && !hasGlobalUsersRead)
            {
                throw CreateValidationException("projectId", "Select a project before managing project-scoped users.");
            }

            return null;
        }

        return MapUser(user, requestContext.ProjectId, includeGlobalAssignments: hasGlobalUsersRead);
    }

    public async Task<UserModel> CreateUserAsync(
        string userName,
        string email,
        string password,
        Guid[]? globalRoleIds,
        Guid[]? projectRoleIds,
        CancellationToken cancellationToken)
    {
        ValidateUser(userName, email, password);

        var requestContext = requestContextAccessor.GetCurrent();
        var canManageGlobalUsers = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.UsersWrite, cancellationToken);
        var normalizedGlobalRoleIds = NormalizeRoleIds(globalRoleIds);
        var normalizedProjectRoleIds = NormalizeRoleIds(projectRoleIds);
        var normalizedUserName = userName.Trim();
        var normalizedEmail = email.Trim();

        if (normalizedGlobalRoleIds.Length == 0 && normalizedProjectRoleIds.Length == 0)
        {
            throw CreateValidationException("roles", "Assign at least one IAM or project role.");
        }

        if (normalizedProjectRoleIds.Length > 0 && requestContext.ProjectId is null)
        {
            throw CreateValidationException("projectRoleIds", "Select a project before assigning project-scoped roles.");
        }

        if (normalizedGlobalRoleIds.Length > 0 && !canManageGlobalUsers)
        {
            throw new ApplicationForbiddenException("Global users.write is required to assign IAM roles.");
        }

        var globalRoles = await LoadRolesAsync(normalizedGlobalRoleIds, RoleScope.Global, null, cancellationToken);
        var projectRoles = await LoadRolesAsync(normalizedProjectRoleIds, RoleScope.Project, requestContext.ProjectId, cancellationToken);

        if (globalRoles.Count != normalizedGlobalRoleIds.Length)
        {
            throw CreateValidationException("globalRoleIds", "One or more specified IAM roles do not exist.");
        }

        if (projectRoles.Count != normalizedProjectRoleIds.Length)
        {
            throw CreateValidationException("projectRoleIds", "One or more specified project-scoped roles do not exist in the active project.");
        }

        if (globalRoles.Any(role => role.IsProtected))
        {
            throw new ApplicationConflictException("Protected IAM roles cannot be assigned.");
        }

        var duplicateExists = await dbContext.Users
            .AnyAsync(
                user => user.UserName == normalizedUserName || user.Email == normalizedEmail,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A user with the same username or email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = normalizedUserName,
            Email = normalizedEmail
        };

        user.PasswordHash = userPasswordService.HashPassword(user, password);
        dbContext.Users.Add(user);

        foreach (var role in globalRoles)
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        foreach (var role in projectRoles)
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id,
                ProjectId = requestContext.ProjectId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var createdUser = await LoadUserQuery()
            .AsNoTracking()
            .FirstAsync(currentUser => currentUser.Id == user.Id, cancellationToken);

        return MapUser(createdUser, requestContext.ProjectId, includeGlobalAssignments: canManageGlobalUsers);
    }

    public async Task<UserModel?> UpdateUserAsync(
        Guid id,
        string userName,
        string email,
        string? password,
        Guid[]? globalRoleIds,
        Guid[]? projectRoleIds,
        CancellationToken cancellationToken)
    {
        ValidateUser(userName, email, password, passwordRequired: false);

        var requestContext = requestContextAccessor.GetCurrent();
        var canManageGlobalUsers = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.UsersWrite, cancellationToken);
        var user = await LoadUserQuery()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.IsProtected)
        {
            throw new ApplicationConflictException("Protected accounts cannot be modified.");
        }

        var userVisibleInProject = requestContext.ProjectId.HasValue && user.RoleAssignments.Any(assignment =>
            assignment.ProjectId == requestContext.ProjectId.Value
            && assignment.Role.Scope == RoleScope.Project
            && assignment.Role.ProjectId == requestContext.ProjectId.Value);

        if (!canManageGlobalUsers && !userVisibleInProject)
        {
            if (requestContext.ProjectId is null)
            {
                throw CreateValidationException("projectId", "Select a project before managing project-scoped users.");
            }

            return null;
        }

        var requestedGlobalRoleIds = canManageGlobalUsers
            ? NormalizeRoleIds(globalRoleIds)
            : [];
        var requestedProjectRoleIds = NormalizeRoleIds(projectRoleIds);
        var normalizedUserName = userName.Trim();
        var normalizedEmail = email.Trim();

        if (requestedProjectRoleIds.Length > 0 && requestContext.ProjectId is null)
        {
            throw CreateValidationException("projectRoleIds", "Select a project before assigning project-scoped roles.");
        }

        if ((globalRoleIds ?? []).Length > 0 && !canManageGlobalUsers)
        {
            throw new ApplicationForbiddenException("Global users.write is required to assign IAM roles.");
        }

        var globalRoles = canManageGlobalUsers
            ? await LoadRolesAsync(requestedGlobalRoleIds, RoleScope.Global, null, cancellationToken)
            : [];
        var projectRoles = await LoadRolesAsync(requestedProjectRoleIds, RoleScope.Project, requestContext.ProjectId, cancellationToken);

        if (globalRoles.Count != requestedGlobalRoleIds.Length)
        {
            throw CreateValidationException("globalRoleIds", "One or more specified IAM roles do not exist.");
        }

        if (projectRoles.Count != requestedProjectRoleIds.Length)
        {
            throw CreateValidationException("projectRoleIds", "One or more specified project-scoped roles do not exist in the active project.");
        }

        if (globalRoles.Any(role => role.IsProtected))
        {
            throw new ApplicationConflictException("Protected IAM roles cannot be assigned.");
        }

        var duplicateExists = await dbContext.Users
            .AnyAsync(
                currentUser =>
                    currentUser.Id != id
                    && (currentUser.UserName == normalizedUserName || currentUser.Email == normalizedEmail),
                cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A user with the same username or email already exists.");
        }

        user.UserName = normalizedUserName;
        user.Email = normalizedEmail;

        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = userPasswordService.HashPassword(user, password);
        }

        if (canManageGlobalUsers)
        {
            var globalAssignments = user.RoleAssignments
                .Where(assignment =>
                    assignment.ProjectId == null
                    && assignment.Role.Scope == RoleScope.Global
                    && assignment.Role.ProjectId == null)
                .ToList();

            foreach (var assignment in globalAssignments.Where(assignment => !requestedGlobalRoleIds.Contains(assignment.RoleId)))
            {
                dbContext.UserRoleAssignments.Remove(assignment);
            }

            foreach (var role in globalRoles.Where(role => globalAssignments.All(assignment => assignment.RoleId != role.Id)))
            {
                dbContext.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        if (requestContext.ProjectId.HasValue)
        {
            var projectAssignments = user.RoleAssignments
                .Where(assignment =>
                    assignment.ProjectId == requestContext.ProjectId.Value
                    && assignment.Role.Scope == RoleScope.Project
                    && assignment.Role.ProjectId == requestContext.ProjectId.Value)
                .ToList();

            foreach (var assignment in projectAssignments.Where(assignment => !requestedProjectRoleIds.Contains(assignment.RoleId)))
            {
                dbContext.UserRoleAssignments.Remove(assignment);
            }

            foreach (var role in projectRoles.Where(role => projectAssignments.All(assignment => assignment.RoleId != role.Id)))
            {
                dbContext.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id,
                    ProjectId = requestContext.ProjectId.Value
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedUser = await LoadUserQuery()
            .AsNoTracking()
            .FirstAsync(currentUser => currentUser.Id == user.Id, cancellationToken);

        return MapUser(updatedUser, requestContext.ProjectId, includeGlobalAssignments: canManageGlobalUsers);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();
        var canManageGlobalUsers = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.UsersWrite, cancellationToken);
        var user = await dbContext.Users
            .Include(currentUser => currentUser.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (user.IsProtected)
        {
            throw new ApplicationConflictException("Protected accounts cannot be deleted.");
        }

        if (requestContext.UserId == id)
        {
            throw new ApplicationConflictException("You cannot delete your own account.");
        }

        if (requestContext.ProjectId is null)
        {
            if (!canManageGlobalUsers)
            {
                throw CreateValidationException("projectId", "Select a project before removing a project-scoped user.");
            }

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var projectAssignments = user.RoleAssignments
            .Where(assignment =>
                assignment.ProjectId == requestContext.ProjectId.Value
                && assignment.Role.Scope == RoleScope.Project
                && assignment.Role.ProjectId == requestContext.ProjectId.Value)
            .ToList();

        if (projectAssignments.Count == 0)
        {
            return false;
        }

        dbContext.UserRoleAssignments.RemoveRange(projectAssignments);

        var remainingAssignmentCount = user.RoleAssignments.Count - projectAssignments.Count;

        if (remainingAssignmentCount <= 0)
        {
            dbContext.Users.Remove(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PagedResult<RoleModel>> GetRolesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = NormalizePagination(page, pageSize);
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalRolesRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesRead, cancellationToken);

        var query = dbContext.Roles
            .AsNoTracking()
            .Where(role =>
                (hasGlobalRolesRead && role.ProjectId == null)
                || (requestContext.ProjectId.HasValue && role.ProjectId == requestContext.ProjectId.Value));
        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .Select(role => new RoleModel(
                role.Id,
                role.Name,
                role.Description,
                role.Scope,
                role.ProjectId,
                role.ProjectName,
                role.IsProtected,
                role.HasAllPermissions,
                role.Assignments.Select(assignment => assignment.UserId).Distinct().Count(),
                role.Permissions
                    .OrderBy(permission => permission.Name)
                    .Select(permission => new PermissionModel(
                        permission.Id,
                        permission.Name,
                        permission.Description,
                        permission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                        permission.ProjectId,
                        permission.ProjectName,
                        permission.Roles.Count))
                    .ToArray()))
            .ToListAsync(cancellationToken);

        var items = roles
            .OrderBy(role => BuiltInRoleNames.GetDisplayOrder(role.Name))
            .ThenBy(role => role.Scope == RoleScope.Global ? 0 : 1)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToArray();

        return CreatePagedResult(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<RoleModel?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalRolesRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesRead, cancellationToken);

        return await dbContext.Roles
            .AsNoTracking()
            .Where(currentRole => currentRole.Id == id)
            .Where(currentRole =>
                (hasGlobalRolesRead && currentRole.ProjectId == null)
                || (requestContext.ProjectId.HasValue && currentRole.ProjectId == requestContext.ProjectId.Value))
            .Select(currentRole => new RoleModel(
                currentRole.Id,
                currentRole.Name,
                currentRole.Description,
                currentRole.Scope,
                currentRole.ProjectId,
                currentRole.ProjectName,
                currentRole.IsProtected,
                currentRole.HasAllPermissions,
                currentRole.Assignments.Select(assignment => assignment.UserId).Distinct().Count(),
                currentRole.Permissions
                    .OrderBy(permission => permission.Name)
                    .Select(permission => new PermissionModel(
                        permission.Id,
                        permission.Name,
                        permission.Description,
                        permission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                        permission.ProjectId,
                        permission.ProjectName,
                        permission.Roles.Count))
                    .ToArray()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RoleModel> CreateRoleAsync(
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken)
    {
        ValidateNameAndDescription(name, description);

        var requestContext = requestContextAccessor.GetCurrent();

        if (scope == RoleScope.Global)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global roles.write is required to create IAM roles.");
            }

        }
        else
        {
            if (requestContext.ProjectId is null)
            {
                throw CreateValidationException("projectId", "Select a project before creating a project-scoped role.");
            }
        }

        var projectId = scope == RoleScope.Global ? (Guid?)null : requestContext.ProjectId;
        var projectName = await ResolveProjectNameAsync(projectId, requestContext.AuthorizationHeader, cancellationToken);

        var roleNameExists = await dbContext.Roles
            .AnyAsync(
                role =>
                    role.Scope == scope
                    && role.ProjectId == projectId
                    && role.Name == name,
                cancellationToken);

        if (roleNameExists)
        {
            throw new ApplicationConflictException("A role with the same scope and name already exists in this context.");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            Scope = scope,
            ProjectId = projectId,
            ProjectName = projectName,
            HasAllPermissions = false,
            IsProtected = false
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await SelectRoleAsync(role.Id, cancellationToken);
    }

    public async Task<RoleModel?> UpdateRoleAsync(
        Guid id,
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken)
    {
        ValidateNameAndDescription(name, description);

        var role = await dbContext.Roles
            .Include(currentRole => currentRole.Assignments)
            .Include(currentRole => currentRole.Permissions)
            .FirstOrDefaultAsync(currentRole => currentRole.Id == id, cancellationToken);

        if (role is null)
        {
            return null;
        }

        if (role.IsProtected)
        {
            throw new ApplicationConflictException("Protected roles cannot be modified.");
        }

        if (scope != role.Scope)
        {
            throw CreateValidationException("scope", "Role scope cannot be changed.");
        }

        var requestContext = requestContextAccessor.GetCurrent();

        if (role.ProjectId is null)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global roles.write is required to update IAM roles.");
            }

        }
        else
        {
            if (requestContext.ProjectId != role.ProjectId)
            {
                throw CreateValidationException("projectId", "Select the owning project before updating this role.");
            }
        }

        var roleNameExists = await dbContext.Roles
            .AnyAsync(
                currentRole =>
                    currentRole.Id != id
                    && currentRole.Scope == role.Scope
                    && currentRole.ProjectId == role.ProjectId
                    && currentRole.Name == name,
                cancellationToken);

        if (roleNameExists)
        {
            throw new ApplicationConflictException("A role with the same scope and name already exists in this context.");
        }

        role.Name = name.Trim();
        role.Description = description.Trim();
        role.HasAllPermissions = false;
        role.ProjectName = await ResolveProjectNameAsync(role.ProjectId, requestContext.AuthorizationHeader, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await SelectRoleAsync(role.Id, cancellationToken);
    }

    public async Task<RoleModel?> UpdateRolePermissionsAsync(Guid id, Guid[]? permissionIds, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(currentRole => currentRole.Assignments)
            .Include(currentRole => currentRole.Permissions)
            .FirstOrDefaultAsync(currentRole => currentRole.Id == id, cancellationToken);

        if (role is null)
        {
            return null;
        }

        if (role.IsProtected)
        {
            throw new ApplicationConflictException("Protected roles cannot be modified.");
        }

        var requestContext = requestContextAccessor.GetCurrent();
        Guid? permissionProjectId;

        if (role.ProjectId is null)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global roles.write is required to update IAM role permissions.");
            }

            permissionProjectId = null;
        }
        else
        {
            if (requestContext.ProjectId != role.ProjectId)
            {
                throw CreateValidationException("projectId", "Select the owning project before updating this role.");
            }

            permissionProjectId = role.ProjectId;
        }

        var normalizedPermissionIds = (permissionIds ?? [])
            .Where(permissionId => permissionId != Guid.Empty)
            .Distinct()
            .ToArray();

        var permissions = await dbContext.Permissions
            .Include(permission => permission.Roles)
            .Where(permission =>
                permission.ProjectId == permissionProjectId
                && normalizedPermissionIds.Contains(permission.Id))
            .ToListAsync(cancellationToken);

        if (permissions.Count != normalizedPermissionIds.Length)
        {
            throw CreateValidationException("permissionIds", "One or more specified permissions do not exist in this context.");
        }

        role.Permissions.Clear();

        foreach (var permission in permissions.OrderBy(permission => permission.Name))
        {
            role.Permissions.Add(permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await SelectRoleAsync(role.Id, cancellationToken);
    }

    public async Task<bool> DeleteRoleAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(currentRole => currentRole.Assignments)
            .FirstOrDefaultAsync(currentRole => currentRole.Id == id, cancellationToken);

        if (role is null)
        {
            return false;
        }

        if (role.IsProtected)
        {
            throw new ApplicationConflictException("Protected roles cannot be deleted.");
        }

        var requestContext = requestContextAccessor.GetCurrent();

        if (role.ProjectId is null)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.RolesWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global roles.write is required to delete IAM roles.");
            }
        }
        else if (requestContext.ProjectId != role.ProjectId)
        {
            throw CreateValidationException("projectId", "Select the owning project before deleting this role.");
        }

        if (role.Assignments.Count > 0)
        {
            throw new ApplicationConflictException("The role is still assigned to one or more users.");
        }

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PagedResult<PermissionModel>> GetPermissionsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = NormalizePagination(page, pageSize);
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalPermissionsRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.PermissionsRead, cancellationToken);

        var query = dbContext.Permissions
            .AsNoTracking()
            .Where(permission =>
                (hasGlobalPermissionsRead && permission.ProjectId == null)
                || (requestContext.ProjectId.HasValue && permission.ProjectId == requestContext.ProjectId.Value));
        var totalCount = await query.CountAsync(cancellationToken);

        var permissions = await query
            .OrderBy(permission => permission.ProjectId.HasValue)
            .ThenBy(permission => permission.Name)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(permission => new PermissionModel(
                permission.Id,
                permission.Name,
                permission.Description,
                permission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                permission.ProjectId,
                permission.ProjectName,
                permission.Roles.Count))
            .ToListAsync(cancellationToken);

        return CreatePagedResult(permissions, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<PermissionModel?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();
        var hasGlobalPermissionsRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.PermissionsRead, cancellationToken);

        return await dbContext.Permissions
            .AsNoTracking()
            .Where(currentPermission => currentPermission.Id == id)
            .Where(currentPermission =>
                (hasGlobalPermissionsRead && currentPermission.ProjectId == null)
                || (requestContext.ProjectId.HasValue && currentPermission.ProjectId == requestContext.ProjectId.Value))
            .Select(currentPermission => new PermissionModel(
                currentPermission.Id,
                currentPermission.Name,
                currentPermission.Description,
                currentPermission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                currentPermission.ProjectId,
                currentPermission.ProjectName,
                currentPermission.Roles.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PermissionModel> CreatePermissionAsync(
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken)
    {
        ValidateNameAndDescription(name, description);

        var requestContext = requestContextAccessor.GetCurrent();
        var normalizedName = name.Trim();
        var normalizedDescription = description.Trim();
        Guid? projectId;

        if (scope == RoleScope.Global)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.PermissionsWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global permissions.write is required to manage IAM permissions.");
            }

            projectId = null;
        }
        else
        {
            if (requestContext.ProjectId is null)
            {
                throw CreateValidationException("projectId", "Select a project before creating a project-scoped permission.");
            }

            projectId = requestContext.ProjectId.Value;
        }

        var projectName = await ResolveProjectNameAsync(projectId, requestContext.AuthorizationHeader, cancellationToken);

        var duplicateExists = await dbContext.Permissions
            .AnyAsync(
                permission => permission.ProjectId == projectId && permission.Name == normalizedName,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A permission with the same name already exists in this context.");
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = normalizedDescription,
            ProjectId = projectId,
            ProjectName = projectName
        };

        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await SelectPermissionAsync(permission.Id, cancellationToken);
    }

    public async Task<PermissionModel?> UpdatePermissionAsync(
        Guid id,
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken)
    {
        ValidateNameAndDescription(name, description);

        var permission = await dbContext.Permissions
            .Include(currentPermission => currentPermission.Roles)
            .FirstOrDefaultAsync(currentPermission => currentPermission.Id == id, cancellationToken);

        if (permission is null)
        {
            return null;
        }

        var currentScope = permission.ProjectId == null ? RoleScope.Global : RoleScope.Project;

        if (scope != currentScope)
        {
            throw CreateValidationException("scope", "Permission scope cannot be changed.");
        }

        var requestContext = requestContextAccessor.GetCurrent();
        var normalizedName = name.Trim();
        var normalizedDescription = description.Trim();

        if (permission.ProjectId is null)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.PermissionsWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global permissions.write is required to manage IAM permissions.");
            }
        }
        else if (requestContext.ProjectId != permission.ProjectId)
        {
            throw CreateValidationException("projectId", "Select the owning project before updating this permission.");
        }

        var duplicateExists = await dbContext.Permissions
            .AnyAsync(
                currentPermission =>
                    currentPermission.Id != id
                    && currentPermission.ProjectId == permission.ProjectId
                    && currentPermission.Name == normalizedName,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A permission with the same name already exists in this context.");
        }

        permission.Name = normalizedName;
        permission.Description = normalizedDescription;
        permission.ProjectName = await ResolveProjectNameAsync(permission.ProjectId, requestContext.AuthorizationHeader, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await SelectPermissionAsync(permission.Id, cancellationToken);
    }

    public async Task<bool> DeletePermissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var permission = await dbContext.Permissions
            .Include(currentPermission => currentPermission.Roles)
            .FirstOrDefaultAsync(currentPermission => currentPermission.Id == id, cancellationToken);

        if (permission is null)
        {
            return false;
        }

        var requestContext = requestContextAccessor.GetCurrent();

        if (permission.ProjectId is null)
        {
            if (!await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.PermissionsWrite, cancellationToken))
            {
                throw new ApplicationForbiddenException("Global permissions.write is required to manage IAM permissions.");
            }
        }
        else if (requestContext.ProjectId != permission.ProjectId)
        {
            throw CreateValidationException("projectId", "Select the owning project before deleting this permission.");
        }

        if (permission.Roles.Count > 0)
        {
            throw new ApplicationConflictException("The permission is still assigned to one or more roles.");
        }

        dbContext.Permissions.Remove(permission);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PagedResult<ProjectModel>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = NormalizePagination(page, pageSize);
        var projects = await LoadProjectsAsync(cancellationToken);

        return CreatePagedResult(
            projects
                .Skip(skip)
                .Take(normalizedPageSize)
                .ToArray(),
            normalizedPage,
            normalizedPageSize,
            projects.Count);
    }

    public async Task<ProjectModel?> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();

        if (requestContext.UserId is null)
        {
            throw new ApplicationUnauthorizedException("Current user context is missing.");
        }

        var projects = await LoadProjectsAsync(cancellationToken);
        return projects.FirstOrDefault(project => project.Id == id);
    }

    public async Task<PagedResult<AuthenticationAuditModel>> GetAuthenticationAuditsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = NormalizePagination(page, pageSize);
        var requestContext = requestContextAccessor.GetCurrent();

        if (requestContext.UserId is null)
        {
            throw new ApplicationUnauthorizedException("Current user context is missing.");
        }

        var hasGlobalAuditRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.ReportsRead, cancellationToken);

        if (!hasGlobalAuditRead && requestContext.ProjectId is null)
        {
            throw CreateValidationException("projectId", "Select a project before viewing project-scoped authentication audit activity.");
        }

        var auditQuery = dbContext.AuthenticationAuditEntries
            .AsNoTracking();

        if (!hasGlobalAuditRead && requestContext.ProjectId.HasValue)
        {
            auditQuery = auditQuery.Where(entry => entry.ProjectId == requestContext.ProjectId.Value);
        }

        auditQuery = auditQuery.OrderByDescending(entry => entry.OccurredAtUtc);
        var totalCount = await auditQuery.CountAsync(cancellationToken);
        var auditEntries = await auditQuery
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);
        var accessibleProjects = await TryGetAccessibleProjectsAsync(
            requestContext.AuthorizationHeader,
            requestContext.ProjectId,
            cancellationToken);

        var projectIds = auditEntries
            .Where(entry => entry.ProjectId.HasValue)
            .Select(entry => entry.ProjectId!.Value)
            .Distinct()
            .ToArray();
        var projectNameLookup = await LoadAuditProjectNameLookupAsync(
            accessibleProjects,
            projectIds,
            cancellationToken);

        var items = auditEntries
            .Select(entry => new AuthenticationAuditModel(
                entry.Id,
                entry.OccurredAtUtc,
                entry.Action,
                entry.Outcome,
                entry.UserId,
                entry.UserNameOrEmail,
                entry.ProjectId,
                entry.ProjectId.HasValue
                    ? projectNameLookup.GetValueOrDefault(entry.ProjectId.Value, entry.ProjectId.Value.ToString())
                    : null,
                entry.FailureReason,
                ResolveClientApplicationName(entry.ClientApplication, accessibleProjects),
                entry.IpAddress,
                entry.UserAgent))
            .ToArray();

        return CreatePagedResult(items, normalizedPage, normalizedPageSize, totalCount);
    }

    private IQueryable<User> LoadUserQuery()
    {
        return dbContext.Users
            .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                    .ThenInclude(role => role.Permissions);
    }

    private async Task<List<Role>> LoadRolesAsync(
        Guid[] roleIds,
        RoleScope scope,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (roleIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Roles
            .Include(role => role.Permissions)
            .Where(role =>
                role.Scope == scope
                && role.ProjectId == projectId
                && roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<RoleModel> SelectRoleAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .Where(currentRole => currentRole.Id == id)
            .Select(currentRole => new RoleModel(
                currentRole.Id,
                currentRole.Name,
                currentRole.Description,
                currentRole.Scope,
                currentRole.ProjectId,
                currentRole.ProjectName,
                currentRole.IsProtected,
                currentRole.HasAllPermissions,
                currentRole.Assignments.Select(assignment => assignment.UserId).Distinct().Count(),
                currentRole.Permissions
                    .OrderBy(permission => permission.Name)
                    .Select(permission => new PermissionModel(
                        permission.Id,
                        permission.Name,
                        permission.Description,
                        permission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                        permission.ProjectId,
                        permission.ProjectName,
                        permission.Roles.Count))
                    .ToArray()))
            .SingleAsync(cancellationToken);
    }

    private async Task<PermissionModel> SelectPermissionAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Id == id)
            .Select(permission => new PermissionModel(
                permission.Id,
                permission.Name,
                permission.Description,
                permission.ProjectId == null ? RoleScope.Global : RoleScope.Project,
                permission.ProjectId,
                permission.ProjectName,
                permission.Roles.Count))
            .SingleAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<PmProjectCatalogProject>> TryGetAccessibleProjectsAsync(
        string? authorizationHeader,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return [];
        }

        try
        {
            return await pmProjectCatalogService.GetAccessibleProjectsAsync(authorizationHeader, projectId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadAuditProjectNameLookupAsync(
        IReadOnlyList<PmProjectCatalogProject> accessibleProjects,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var localLookup = await LoadProjectNameLookupAsync(projectIds, cancellationToken);

        if (accessibleProjects.Count == 0)
        {
            return localLookup;
        }

        var projectIdSet = projectIds.ToHashSet();
        var remoteLookup = accessibleProjects
            .Where(project => projectIdSet.Contains(project.Id))
            .GroupBy(project => project.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Select(project => project.Name)
                    .FirstOrDefault(projectName => !string.IsNullOrWhiteSpace(projectName))
                    ?? localLookup.GetValueOrDefault(group.Key, group.Key.ToString()));

        return projectIds.ToDictionary(
            projectId => projectId,
            projectId => remoteLookup.GetValueOrDefault(
                projectId,
                localLookup.GetValueOrDefault(projectId, projectId.ToString())));
    }

    private async Task<IReadOnlyList<ProjectModel>> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();

        if (requestContext.UserId is null)
        {
            throw new ApplicationUnauthorizedException("Current user context is missing.");
        }

        var hasGlobalProjectsRead = await HasGlobalPermissionAsync(requestContext.UserId, PermissionNames.ProjectsRead, cancellationToken);
        var accessibleProjects = await TryGetAccessibleProjectsAsync(
            requestContext.AuthorizationHeader,
            requestContext.ProjectId,
            cancellationToken);

        if (accessibleProjects.Count == 0)
        {
            return await LoadLocalProjectsAsync(requestContext.UserId, hasGlobalProjectsRead, cancellationToken);
        }

        var projectIds = accessibleProjects.Select(project => project.Id).ToArray();
        var memberCounts = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId.HasValue && projectIds.Contains(assignment.ProjectId.Value))
            .GroupBy(assignment => assignment.ProjectId!.Value)
            .Select(group => new { ProjectId = group.Key, MemberCount = group.Select(assignment => assignment.UserId).Distinct().Count() })
            .ToDictionaryAsync(group => group.ProjectId, group => group.MemberCount, cancellationToken);

        return accessibleProjects
            .OrderBy(project => project.Name)
            .Select(project => new ProjectModel(
                project.Id,
                project.Name,
                project.Description,
                memberCounts.GetValueOrDefault(project.Id)))
            .ToArray();
    }

    private static string? ResolveClientApplicationName(
        string? clientApplication,
        IReadOnlyList<PmProjectCatalogProject> accessibleProjects)
    {
        if (string.IsNullOrWhiteSpace(clientApplication))
        {
            return null;
        }

        var normalizedClientApplication = clientApplication.Trim();
        var resolvedName = accessibleProjects
            .FirstOrDefault(project =>
                string.Equals(project.Code, normalizedClientApplication, StringComparison.OrdinalIgnoreCase)
                || string.Equals(project.Name, normalizedClientApplication, StringComparison.OrdinalIgnoreCase))
            ?.Name;

        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            return resolvedName;
        }

        return normalizedClientApplication;
    }

    private async Task<IReadOnlyList<ProjectModel>> LoadLocalProjectsAsync(
        Guid? userId,
        bool hasGlobalProjectsRead,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ProjectId.HasValue
                && (hasGlobalProjectsRead || (userId.HasValue && assignment.UserId == userId.Value)))
            .Select(assignment => new { ProjectId = assignment.ProjectId!.Value, assignment.UserId })
            .ToListAsync(cancellationToken);
        var roleProjects = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.ProjectId.HasValue)
            .Select(role => new { ProjectId = role.ProjectId!.Value, role.ProjectName })
            .ToListAsync(cancellationToken);
        var permissionProjects = await dbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.ProjectId.HasValue)
            .Select(permission => new { ProjectId = permission.ProjectId!.Value, permission.ProjectName })
            .ToListAsync(cancellationToken);

        var projectIds = assignments
            .Select(assignment => assignment.ProjectId)
            .Concat(roleProjects.Select(role => role.ProjectId))
            .Concat(permissionProjects.Select(permission => permission.ProjectId))
            .Distinct()
            .ToArray();
        var memberCounts = assignments
            .GroupBy(assignment => assignment.ProjectId)
            .ToDictionary(group => group.Key, group => group.Select(assignment => assignment.UserId).Distinct().Count());

        return projectIds
            .Select(projectId =>
            {
                var projectName = roleProjects
                    .Where(role => role.ProjectId == projectId)
                    .Select(role => role.ProjectName)
                    .Concat(permissionProjects.Where(permission => permission.ProjectId == projectId).Select(permission => permission.ProjectName))
                    .FirstOrDefault(projectName => !string.IsNullOrWhiteSpace(projectName))
                    ?? projectId.ToString();

                return new ProjectModel(
                    projectId,
                    projectName,
                    "Project metadata is temporarily unavailable from ProjectX.PM.",
                    memberCounts.GetValueOrDefault(projectId));
            })
            .OrderBy(project => project.Name)
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadProjectNameLookupAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var roleProjects = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.ProjectId.HasValue && projectIds.Contains(role.ProjectId.Value) && role.ProjectName != null)
            .Select(role => new { ProjectId = role.ProjectId!.Value, ProjectName = role.ProjectName! })
            .ToListAsync(cancellationToken);
        var permissionProjects = await dbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.ProjectId.HasValue && projectIds.Contains(permission.ProjectId.Value) && permission.ProjectName != null)
            .Select(permission => new { ProjectId = permission.ProjectId!.Value, ProjectName = permission.ProjectName! })
            .ToListAsync(cancellationToken);

        return roleProjects
            .Select(project => (project.ProjectId, project.ProjectName))
            .Concat(permissionProjects.Select(project => (project.ProjectId, project.ProjectName)))
            .GroupBy(project => project.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(project => project.ProjectName)
                    .FirstOrDefault(projectName => !string.IsNullOrWhiteSpace(projectName))
                    ?? group.Key.ToString());
    }

    private async Task<string?> ResolveProjectNameAsync(
        Guid? projectId,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return null;
        }

        var accessibleProjectName = (await TryGetAccessibleProjectsAsync(authorizationHeader, projectId, cancellationToken))
            .FirstOrDefault(project => project.Id == projectId.Value)
            ?.Name;

        if (!string.IsNullOrWhiteSpace(accessibleProjectName))
        {
            return accessibleProjectName;
        }

        var knownProjectName = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.ProjectId == projectId.Value && role.ProjectName != null)
            .Select(role => role.ProjectName)
            .Concat(dbContext.Permissions
                .AsNoTracking()
                .Where(permission => permission.ProjectId == projectId.Value && permission.ProjectName != null)
                .Select(permission => permission.ProjectName))
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(knownProjectName)
            ? projectId.Value.ToString()
            : knownProjectName;
    }

    private static (int Page, int PageSize, int Skip) NormalizePagination(int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return (normalizedPage, normalizedPageSize, (normalizedPage - 1) * normalizedPageSize);
    }

    private static PagedResult<T> CreatePagedResult<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<T>(items, page, pageSize, totalCount, totalPages);
    }

    private async Task<bool> HasGlobalPermissionAsync(Guid? userId, string permission, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return false;
        }

        return await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == userId.Value
                && assignment.ProjectId == null
                && assignment.Role.Scope == RoleScope.Global
                && assignment.Role.ProjectId == null)
            .AnyAsync(
                assignment =>
                    assignment.Role.HasAllPermissions
                    || assignment.Role.Permissions.Any(currentPermission =>
                        currentPermission.ProjectId == null
                        && currentPermission.Name == permission),
                cancellationToken);
    }

    private static bool CanAccessUser(User user, Guid? activeProjectId, bool hasGlobalUsersRead)
    {
        if (hasGlobalUsersRead && activeProjectId is null)
        {
            return true;
        }

        return user.RoleAssignments.Any(assignment =>
            (hasGlobalUsersRead
                && assignment.ProjectId == null
                && assignment.Role.Scope == RoleScope.Global
                && assignment.Role.ProjectId == null)
            || (activeProjectId.HasValue
                && assignment.ProjectId == activeProjectId.Value
                && assignment.Role.Scope == RoleScope.Project
                && assignment.Role.ProjectId == activeProjectId.Value));
    }

    private static Guid[] NormalizeRoleIds(Guid[]? roleIds)
    {
        return (roleIds ?? [])
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private static void ValidateUser(string userName, string email, string? password, bool passwordRequired = true)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors["userName"] = ["Username is required."];
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["Email is required."];
        }

        if (passwordRequired && string.IsNullOrWhiteSpace(password))
        {
            errors["password"] = ["Password is required."];
        }

        ThrowIfValidationErrors(errors);
    }

    private static void ValidateNameAndDescription(string name, string description)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }

        ThrowIfValidationErrors(errors);
    }

    private static void ThrowIfValidationErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static ApplicationValidationException CreateValidationException(string key, string message)
    {
        return new ApplicationValidationException(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
    }

    private static UserModel MapUser(User user, Guid? activeProjectId, bool includeGlobalAssignments)
    {
        var visibleAssignments = user.RoleAssignments
            .Where(assignment =>
                (includeGlobalAssignments
                    && assignment.ProjectId == null
                    && assignment.Role.Scope == RoleScope.Global
                    && assignment.Role.ProjectId == null)
                || (activeProjectId.HasValue
                    && assignment.ProjectId == activeProjectId.Value
                    && assignment.Role.Scope == RoleScope.Project
                    && assignment.Role.ProjectId == activeProjectId.Value))
            .OrderBy(assignment => BuiltInRoleNames.GetDisplayOrder(assignment.Role.Name))
            .ThenBy(assignment => assignment.ProjectId.HasValue ? 1 : 0)
            .ThenBy(assignment => assignment.Role.Name, StringComparer.OrdinalIgnoreCase)
            .Select(assignment => new UserRoleAssignmentModel(
                assignment.Id,
                assignment.RoleId,
                assignment.Role.Name,
                assignment.Role.Scope,
                assignment.ProjectId,
                assignment.Role.ProjectName,
                assignment.Role.IsProtected,
                assignment.Role.HasAllPermissions,
                assignment.Role.Permissions
                    .Where(permission => permission.ProjectId == assignment.Role.ProjectId)
                    .Select(permission => permission.Name)
                    .Order()
                    .ToArray()))
            .ToArray();

        var effectivePermissions = visibleAssignments
            .SelectMany(assignment => assignment.Permissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToArray();

        return new UserModel(
            user.Id,
            user.IsProtected,
            user.UserName,
            user.Email,
            visibleAssignments,
            effectivePermissions);
    }
}

