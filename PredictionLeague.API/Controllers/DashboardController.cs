using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Dashboard.Queries;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leagues;

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

    [HttpGet("upcoming-rounds")]
    [ProducesResponseType(typeof(IEnumerable<UpcomingRoundDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UpcomingRoundDto>>> GetUpcomingRoundsAsync(CancellationToken cancellationToken)
    {
        var query = new GetUpcomingRoundsQuery(CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("my-leagues")]
    [ProducesResponseType(typeof(IEnumerable<MyLeagueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MyLeagueDto>>> GetMyLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new GetMyLeaguesQuery(CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("available-leagues")]
    [ProducesResponseType(typeof(IEnumerable<AvailableLeagueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AvailableLeagueDto>>> GetAvailableLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new GetAvailableLeaguesQuery(CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }
}