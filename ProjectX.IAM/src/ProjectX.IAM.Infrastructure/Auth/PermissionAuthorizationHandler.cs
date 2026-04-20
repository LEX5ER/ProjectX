using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectX.IAM.Domain.Entities;
using ProjectX.IAM.Infrastructure.Persistence;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class PermissionAuthorizationHandler(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = GetCurrentUserId(context.User);

        if (userId is null)
        {
            return;
        }

        var projectId = GetProjectId(httpContextAccessor.HttpContext);

        var hasPermission = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == userId.Value
                && ((assignment.Role.Scope == RoleScope.Global
                        && assignment.ProjectId == null
                        && assignment.Role.ProjectId == null)
                    || (projectId.HasValue
                        && assignment.Role.Scope == RoleScope.Project
                        && assignment.ProjectId == projectId.Value
                        && assignment.Role.ProjectId == projectId.Value)))
            .AnyAsync(assignment =>
                assignment.Role.HasAllPermissions
                || assignment.Role.Permissions.Any(permission =>
                    permission.Name == requirement.Permission
                    && permission.ProjectId == assignment.Role.ProjectId));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var rawValue = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawValue, out var userId) ? userId : null;
    }

    private static Guid? GetProjectId(HttpContext? httpContext)
    {
        var rawValue = httpContext?.Request.Headers[ProjectContextHeaderNames.ProjectId].ToString();
        return Guid.TryParse(rawValue, out var projectId) ? projectId : null;
    }
}
