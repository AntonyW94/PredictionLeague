using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Rounds;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Rounds - View round and match information")]
public class RoundsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}/matches-data")]
    [ProducesResponseType(typeof(IEnumerable<MatchInRoundDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get matches for a round",
        Description = "Returns all matches in the specified round with team details, kick-off times, and current scores.")]
    [SwaggerResponse(200, "Matches retrieved successfully", typeof(IEnumerable<MatchInRoundDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<IEnumerable<MatchInRoundDto>>> GetMatchesForRoundAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetMatchesForRoundQuery(roundId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }
}
