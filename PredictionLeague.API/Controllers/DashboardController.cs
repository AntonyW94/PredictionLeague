using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("dashboard-data")]
    public async Task<IActionResult> GetDashboardData()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(await _dashboardService.GetDashboardDataAsync(userId!));
    }
}