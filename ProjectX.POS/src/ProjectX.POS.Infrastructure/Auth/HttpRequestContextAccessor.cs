using Microsoft.AspNetCore.Http;
using ProjectX.POS.Application.Abstractions;

namespace ProjectX.POS.Infrastructure.Auth;

public sealed class HttpRequestContextAccessor(IHttpContextAccessor httpContextAccessor) : IRequestContextAccessor
{
    public RequestContext GetCurrent()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return new RequestContext(null, null);
        }

        var projectId = httpContext.Request.Headers[ProjectContextHeaderNames.ProjectId].ToString();

        return new RequestContext(
            httpContext.Request.Headers.Authorization.ToString(),
            projectId);
    }
}
