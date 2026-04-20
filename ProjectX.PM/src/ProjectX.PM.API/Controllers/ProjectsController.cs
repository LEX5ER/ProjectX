using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.PM.API.Contracts.Projects;
using ProjectX.PM.Application.Projects;

namespace ProjectX.PM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("catalog")]
    public async Task<ActionResult<PagedResult<ProjectModel>>> GetCatalog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await projectsService.GetProjectCatalogAsync(page, pageSize, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await projectsService.GetProjectsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var project = await projectsService.GetProjectByIdAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectModel>> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectsService.CreateProjectAsync(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerName,
            request.Status,
            request.StartDate,
            request.TargetDate,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectModel>> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectsService.UpdateProjectAsync(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.OwnerName,
            request.Status,
            request.StartDate,
            request.TargetDate,
            cancellationToken);

        return project is null ? NotFound() : Ok(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await projectsService.DeleteProjectAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
