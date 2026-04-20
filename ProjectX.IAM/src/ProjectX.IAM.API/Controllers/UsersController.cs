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
public sealed class UsersController(IIdentityAdministrationService identityAdministrationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(PermissionNames.UsersRead)]
    public async Task<ActionResult<PagedResult<UserModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await identityAdministrationService.GetUsersAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [PermissionAuthorize(PermissionNames.UsersRead)]
    public async Task<ActionResult<UserModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await identityAdministrationService.GetUserByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [PermissionAuthorize(PermissionNames.UsersWrite)]
    public async Task<ActionResult<UserModel>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await identityAdministrationService.CreateUserAsync(
            request.UserName,
            request.Email,
            request.Password,
            request.GlobalRoleIds,
            request.ProjectRoleIds,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [PermissionAuthorize(PermissionNames.UsersWrite)]
    public async Task<ActionResult<UserModel>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await identityAdministrationService.UpdateUserAsync(
            id,
            request.UserName,
            request.Email,
            request.Password,
            request.GlobalRoleIds,
            request.ProjectRoleIds,
            cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [PermissionAuthorize(PermissionNames.UsersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await identityAdministrationService.DeleteUserAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
