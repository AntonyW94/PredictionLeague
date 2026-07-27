using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Application.Features.Sharing.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Rounds - View round and match information")]
public class RoundsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{roundId:int}/matches-data")]
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
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{roundId:int}/share-card")]
    [SwaggerOperation(
        Summary = "Get a shareable image of the current user's predictions for a round",
        Description = "Renders the calling user's predictions for the round as a branded PNG suitable for sharing via the native share sheet. Returns 404 when the round does not exist or the user has not predicted any of its matches.")]
    [SwaggerResponse(200, "Share card image generated", typeof(FileResult))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "No predictions to share for this round")]
    public async Task<IActionResult> GetShareCardAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var image = await mediator.Send(new GetRoundShareCardImageQuery(roundId, CurrentUserId), cancellationToken);

        if (image is null)
            return NotFound();

        return File(image, "image/png");
    }
}
