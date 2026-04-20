using ProjectX.POS.Application.Abstractions;

namespace ProjectX.POS.Application.Auth;

public interface IIamAuthorizationContextService
{
    Task<PosAuthorizationContext> GetCurrentAsync(RequestContext requestContext, CancellationToken cancellationToken);
}
