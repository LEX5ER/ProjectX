using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Infrastructure.Auth;

namespace ProjectX.IAM.API.Controllers;

[ApiController]
[Route("api/authentication-audits")]
[Authorize]
public sealed class AuthenticationAuditsController(IIdentityAdministrationService identityAdministrationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(PermissionNames.ReportsRead)]
    public async Task<ActionResult<PagedResult<AuthenticationAuditModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await identityAdministrationService.GetAuthenticationAuditsAsync(page, pageSize, cancellationToken));
    }
}
