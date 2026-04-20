using ProjectX.PM.Application.Abstractions;

namespace ProjectX.PM.Application.Auth;

public interface IIamAuthorizationContextService
{
    Task<PmAuthorizationContext> GetCurrentAsync(RequestContext requestContext, CancellationToken cancellationToken);
}
