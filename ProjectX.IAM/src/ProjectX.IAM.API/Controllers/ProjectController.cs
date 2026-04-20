using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Infrastructure.Auth;

namespace ProjectX.IAM.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ProjectController(IIdentityAdministrationService identityAdministrationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(PermissionNames.ProjectsRead)]
    public async Task<ActionResult<PagedResult<ProjectModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await identityAdministrationService.GetProjectsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [PermissionAuthorize(PermissionNames.ProjectsRead)]
    public async Task<ActionResult<ProjectModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var project = await identityAdministrationService.GetProjectByIdAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }
}
