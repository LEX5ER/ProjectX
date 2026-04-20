using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.IAM.API.Contracts.Management;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Infrastructure.Auth;

namespace ProjectX.IAM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PermissionsController(IIdentityAdministrationService identityAdministrationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(PermissionNames.PermissionsRead)]
    public async Task<ActionResult<PagedResult<PermissionModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await identityAdministrationService.GetPermissionsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [PermissionAuthorize(PermissionNames.PermissionsRead)]
    public async Task<ActionResult<PermissionModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var permission = await identityAdministrationService.GetPermissionByIdAsync(id, cancellationToken);
        return permission is null ? NotFound() : Ok(permission);
    }

    [HttpPost]
    [PermissionAuthorize(PermissionNames.PermissionsWrite)]
    public async Task<ActionResult<PermissionModel>> Create([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var permission = await identityAdministrationService.CreatePermissionAsync(
            request.Name,
            request.Description,
            request.Scope,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = permission.Id }, permission);
    }

    [HttpPut("{id:guid}")]
    [PermissionAuthorize(PermissionNames.PermissionsWrite)]
    public async Task<ActionResult<PermissionModel>> Update(Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken cancellationToken)
    {
        var permission = await identityAdministrationService.UpdatePermissionAsync(
            id,
            request.Name,
            request.Description,
            request.Scope,
            cancellationToken);

        return permission is null ? NotFound() : Ok(permission);
    }

    [HttpDelete("{id:guid}")]
    [PermissionAuthorize(PermissionNames.PermissionsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await identityAdministrationService.DeletePermissionAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
