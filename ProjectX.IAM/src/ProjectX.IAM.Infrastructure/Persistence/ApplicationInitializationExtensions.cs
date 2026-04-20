using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Domain.Entities;
using ProjectX.IAM.Infrastructure.Auth;

namespace ProjectX.IAM.Infrastructure.Persistence;

public static class ApplicationInitializationExtensions
{
    public static async Task InitializePersistenceAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedIdentityOptions>>().Value;

        await dbContext.Database.MigrateAsync();

        var allPermissions = await dbContext.Permissions
            .Where(permission => permission.ProjectId == null)
            .ToDictionaryAsync(permission => permission.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var permissionName in PermissionNames.BuiltIn.Where(permissionName => !allPermissions.ContainsKey(permissionName)))
        {
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = permissionName,
                Description = $"Allows {permissionName.Replace('.', ' ')}.",
                ProjectId = null
            };

            allPermissions[permissionName] = permission;
            dbContext.Permissions.Add(permission);
        }

        var globalRoles = await dbContext.Roles
            .Include(role => role.Permissions)
            .Where(role => role.Scope == RoleScope.Global && role.ProjectId == null)
            .ToDictionaryAsync(role => role.Name, StringComparer.OrdinalIgnoreCase);

        var superAdminRole = EnsureGlobalRole(
            BuiltInRoleNames.SuperAdmin,
            "Protected global role with full access to the entire IAM platform.",
            isProtected: true,
            hasAllPermissions: true,
            permissionNames: [],
            dbContext,
            globalRoles,
            allPermissions);

        EnsureGlobalRole(
            BuiltInRoleNames.PmPortfolioAdmin,
            "Global ProjectX.PM portfolio administration without full IAM super admin access.",
            isProtected: false,
            hasAllPermissions: false,
            permissionNames:
            [
                PermissionNames.ProjectsRead,
                PermissionNames.ProjectsWrite
            ],
            dbContext,
            globalRoles,
            allPermissions);

        var administratorUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.UserName == seedOptions.UserName || user.Email == seedOptions.Email);

        if (administratorUser is null)
        {
            administratorUser = new User
            {
                Id = Guid.NewGuid(),
                IsProtected = true,
                UserName = seedOptions.UserName,
                Email = seedOptions.Email
            };

            administratorUser.PasswordHash = passwordHasher.HashPassword(administratorUser, seedOptions.Password);
            dbContext.Users.Add(administratorUser);
        }
        else
        {
            administratorUser.IsProtected = true;
        }

        var hasSuperAdminAssignment = await dbContext.UserRoleAssignments
            .AnyAsync(assignment =>
                assignment.UserId == administratorUser.Id
                && assignment.RoleId == superAdminRole.Id
                && assignment.ProjectId == null);

        if (!hasSuperAdminAssignment)
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = administratorUser.Id,
                RoleId = superAdminRole.Id
            });
        }

        await dbContext.SaveChangesAsync();

        var pmProjectCatalogService = scope.ServiceProvider.GetRequiredService<IPmProjectCatalogService>();

        try
        {
            await pmProjectCatalogService.GetAllProjectsAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            app.Logger.LogWarning(
                exception,
                "Skipping PM project sync during IAM initialization because the PM catalog API is unavailable.");
        }
    }

    private static Role EnsureGlobalRole(
        string name,
        string description,
        bool isProtected,
        bool hasAllPermissions,
        string[] permissionNames,
        ApplicationDbContext dbContext,
        IDictionary<string, Role> globalRoles,
        IReadOnlyDictionary<string, Permission> allPermissions)
    {
        if (!globalRoles.TryGetValue(name, out var role))
        {
            if (role is null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = name
                };

                dbContext.Roles.Add(role);
            }

            globalRoles[name] = role;
        }

        role.Name = name;
        role.Description = description;
        role.Scope = RoleScope.Global;
        role.ProjectId = null;
        role.ProjectName = null;
        role.IsProtected = isProtected;
        role.HasAllPermissions = hasAllPermissions;

        var expectedPermissions = permissionNames
            .Select(permissionName => allPermissions[permissionName])
            .ToArray();

        foreach (var permission in role.Permissions.Where(permission => !expectedPermissions.Contains(permission)).ToArray())
        {
            role.Permissions.Remove(permission);
        }

        foreach (var permission in expectedPermissions.Where(permission => !role.Permissions.Contains(permission)))
        {
            role.Permissions.Add(permission);
        }

        return role;
    }
}
