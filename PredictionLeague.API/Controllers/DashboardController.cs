using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Dashboard.Queries;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard-data")]
    public async Task<IActionResult> GetDashboardDataAsync()
    {
        var query = new GetDashboardDataQuery(CurrentUserId);
        var result = await _mediator.Send(query);

        return Ok(result);
    }
}