using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Dashboard.Queries;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard-data")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> GetDashboardDataAsync(CancellationToken cancellationToken)
    {
        var query = new GetDashboardDataQuery(CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
}