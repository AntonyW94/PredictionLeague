using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RoundsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}/matches-data")]
    [ProducesResponseType(typeof(IEnumerable<MatchInRoundDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MatchInRoundDto>>> GetMatchesForRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var query = new GetMatchesForRoundQuery(roundId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }
}