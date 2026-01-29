using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Constants;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = RoleNames.Administrator)]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Rounds - Manage gameweeks and matches (Admin only)")]
public class RoundsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    [SwaggerOperation(
        Summary = "Create a new round",
        Description = "Creates a new gameweek/round with matches. Rounds start in Draft status and must be published to become visible.")]
    [SwaggerResponse(201, "Round created successfully", typeof(RoundDto))]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> CreateRoundAsync(
        [FromBody, SwaggerParameter("Round configuration with matches", Required = true)] CreateRoundRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRoundCommand(
            request.SeasonId,
            request.RoundNumber,
            request.ApiRoundName,
            request.StartDateUtc,
            request.DeadlineUtc,
            request.Matches
        );

        var newRound = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetRoundById", new { roundId = newRound.Id }, newRound);
    }

    #endregion

    #region Read

    [HttpGet("by-season/{seasonId:int}")]
    [SwaggerOperation(
        Summary = "Get rounds for a season",
        Description = "Returns all rounds/gameweeks for the specified season, ordered by round number.")]
    [SwaggerResponse(200, "Rounds retrieved successfully", typeof(IEnumerable<RoundDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<RoundDto>>> FetchRoundsForSeasonAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var query = new FetchRoundsForSeasonQuery(seasonId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{roundId:int}")]
    [SwaggerOperation(
        Summary = "Get round by ID",
        Description = "Returns detailed information about a round including all matches with teams and scores.")]
    [SwaggerResponse(200, "Round retrieved successfully", typeof(RoundDetailsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<ActionResult<RoundDetailsDto>> GetRoundByIdAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetRoundByIdQuery(roundId);
        var roundDetails = await _mediator.Send(query, cancellationToken);

        if (roundDetails == null)
            return NotFound();

        return Ok(roundDetails);
    }

    #endregion

    #region Update

    [HttpPut("{roundId:int}/update")]
    [SwaggerOperation(
        Summary = "Update round details",
        Description = "Updates a round's configuration including deadline, status, and match list. Can change status from Draft to Published.")]
    [SwaggerResponse(204, "Round updated successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<IActionResult> UpdateRoundAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        [FromBody, SwaggerParameter("Updated round configuration", Required = true)] UpdateRoundRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRoundCommand(
            roundId,
            request.RoundNumber,
            request.ApiRoundName,
            request.StartDateUtc,
            request.DeadlineUtc,
            request.Status,
            request.Matches);

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{roundId:int}/results")]
    [SwaggerOperation(
        Summary = "Submit match results",
        Description = "Updates final scores for matches in a round. Triggers recalculation of predictions and leaderboards.")]
    [SwaggerResponse(204, "Results submitted successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<IActionResult> SubmitResultsAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        [FromBody, SwaggerParameter("Match results", Required = true)] List<MatchResultDto> matches,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMatchResultsCommand(roundId, matches);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
