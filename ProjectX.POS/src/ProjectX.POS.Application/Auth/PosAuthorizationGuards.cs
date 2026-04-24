using ProjectX.POS.Application.Abstractions;

namespace ProjectX.POS.Application.Auth;

internal static class PosAuthorizationGuards
{
    public static async Task<PosAuthorizationContext> GetAuthorizationContextAsync(
        IRequestContextAccessor requestContextAccessor,
        IIamAuthorizationContextService authorizationContextService,
        CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationContextService.GetCurrentAsync(requestContextAccessor.GetCurrent(), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            throw new ApplicationServiceUnavailableException(
                "IAM authorization unavailable",
                "ProjectX.POS could not validate the current IAM project.");
        }
    }

    public static void EnsureCanReadAny(PosAuthorizationContext accessContext, string resourceName)
    {
        if (accessContext.CanReadAnyProjectData)
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeScopedAccessFailure(accessContext, "view", resourceName));
    }

    public static Guid RequireActiveProjectForManagement(PosAuthorizationContext accessContext, string resourceName)
    {
        if (!accessContext.ActiveProjectId.HasValue)
        {
            throw new ApplicationForbiddenException($"Select the POS IAM project before attempting to manage {resourceName}.");
        }

        if (accessContext.CanManageAnyProjectData)
        {
            return accessContext.ActiveProjectId.Value;
        }

        throw new ApplicationForbiddenException(DescribeScopedAccessFailure(accessContext, "manage", resourceName));
    }

    public static void EnsureCanReadProject(PosAuthorizationContext accessContext, Guid projectId, string resourceName)
    {
        if (accessContext.CanReadProject(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProjectAccessFailure(accessContext, projectId, "view", resourceName));
    }

    public static void EnsureCanManageProject(PosAuthorizationContext accessContext, Guid projectId, string resourceName)
    {
        if (accessContext.CanManageProject(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProjectAccessFailure(accessContext, projectId, "manage", resourceName));
    }

    private static string DescribeScopedAccessFailure(PosAuthorizationContext accessContext, string action, string resourceName)
    {
        if (!accessContext.ActiveProjectId.HasValue)
        {
            return $"Select the POS IAM project before attempting to {action} {resourceName}.";
        }

        return $"This session does not have permission to {action} {resourceName} for the active IAM project.";
    }

    private static string DescribeProjectAccessFailure(
        PosAuthorizationContext accessContext,
        Guid projectId,
        string action,
        string resourceName)
    {
        if (accessContext.ActiveProjectId == projectId)
        {
            return $"This session does not have permission to {action} {resourceName} for the active IAM project.";
        }

        if (accessContext.ActiveProjectId.HasValue)
        {
            return $"This session can only {action} {resourceName} linked to the active IAM project.";
        }

        return DescribeScopedAccessFailure(accessContext, action, resourceName);
    }
}
