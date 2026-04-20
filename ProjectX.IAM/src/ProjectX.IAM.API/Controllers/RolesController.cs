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
public sealed class RolesController(IIdentityAdministrationService identityAdministrationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(PermissionNames.RolesRead)]
    public async Task<ActionResult<PagedResult<RoleModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await identityAdministrationService.GetRolesAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [PermissionAuthorize(PermissionNames.RolesRead)]
    public async Task<ActionResult<RoleModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await identityAdministrationService.GetRoleByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [PermissionAuthorize(PermissionNames.RolesWrite)]
    public async Task<ActionResult<RoleModel>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await identityAdministrationService.CreateRoleAsync(
            request.Name,
            request.Description,
            request.Scope,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [PermissionAuthorize(PermissionNames.RolesWrite)]
    public async Task<ActionResult<RoleModel>> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await identityAdministrationService.UpdateRoleAsync(
            id,
            request.Name,
            request.Description,
            request.Scope,
            cancellationToken);

        return role is null ? NotFound() : Ok(role);
    }

    [HttpPut("{id:guid}/permissions")]
    [PermissionAuthorize(PermissionNames.RolesWrite)]
    public async Task<ActionResult<RoleModel>> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await identityAdministrationService.UpdateRolePermissionsAsync(id, request.PermissionIds, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpDelete("{id:guid}")]
    [PermissionAuthorize(PermissionNames.RolesWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await identityAdministrationService.DeleteRoleAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
