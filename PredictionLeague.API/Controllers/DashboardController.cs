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

    [HttpGet("public-leagues")]
    [ProducesResponseType(typeof(IEnumerable<PublicLeagueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublicLeagueDto>>> GetPublicLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new GetPublicLeaguesQuery(CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }
}