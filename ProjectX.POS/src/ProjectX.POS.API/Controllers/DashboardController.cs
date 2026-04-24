using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.POS.Application.Dashboard;

namespace ProjectX.POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryModel>> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await dashboardService.GetSummaryAsync(cancellationToken));
    }
}
