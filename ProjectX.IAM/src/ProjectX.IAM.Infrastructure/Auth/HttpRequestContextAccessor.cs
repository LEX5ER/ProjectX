using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectX.IAM.Application.Abstractions;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class HttpRequestContextAccessor(IHttpContextAccessor httpContextAccessor) : IRequestContextAccessor
{
    public RequestContext GetCurrent()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return new RequestContext(null, null, null);
        }

        return new RequestContext(
            GetCurrentUserId(httpContext.User),
            GetProjectId(httpContext),
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.Request.Headers[ClientApplicationHeaderNames.ClientApplication].ToString(),
            GetIpAddress(httpContext),
            httpContext.Request.Headers.UserAgent.ToString());
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var rawValue = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawValue, out var userId) ? userId : null;
    }

    private static Guid? GetProjectId(HttpContext httpContext)
    {
        var rawValue = httpContext.Request.Headers[ProjectContextHeaderNames.ProjectId].ToString();
        return Guid.TryParse(rawValue, out var projectId) ? projectId : null;
    }

    private static string? GetIpAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
