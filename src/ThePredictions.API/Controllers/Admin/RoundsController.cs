using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Features.Rounds.Commands;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Rounds - Manage gameweeks and matches (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class RoundsController(IMediator mediator) : ApiControllerBase
{
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

        var newRound = await mediator.Send(command, cancellationToken);

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
        return Ok(await mediator.Send(query, cancellationToken));
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
        var roundDetails = await mediator.Send(query, cancellationToken);

        if (roundDetails == null)
            return NotFound();

        return Ok(roundDetails);
    }

    [HttpGet("{roundId:int}/completion")]
    [SwaggerOperation(
        Summary = "Get prediction-completion status for a round",
        Description = "Returns every participant in the round's season with how many predictable fixtures they have entered, which they are still missing, and when they were last reminded. Used by the admin incomplete-predictions view.")]
    [SwaggerResponse(200, "Completion status retrieved successfully", typeof(RoundCompletionDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<ActionResult<RoundCompletionDto>> GetRoundCompletionAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetRoundCompletionQuery(roundId, LeagueId: null, CurrentUserId, IsSiteAdmin: true);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
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

        await mediator.Send(command, cancellationToken);

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
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{roundId:int}/resend-digest")]
    [SwaggerOperation(
        Summary = "Re-send the round-results digest",
        Description = "Forces a re-send of the round-results digest email to every user who predicted in the round, even if it was already sent. The round must be completed.")]
    [SwaggerResponse(204, "Digest re-send triggered")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> ResendDigestAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new SendRoundDigestEmailsCommand(roundId, Force: true), cancellationToken);

        return NoContent();
    }

    [HttpPost("{roundId:int}/resend-prize-emails")]
    [SwaggerOperation(
        Summary = "Re-send the prize-won emails",
        Description = "Forces a re-send of the celebratory \"Prize Won\" email to every winner in the round's season, even if they were already notified. The round must be completed.")]
    [SwaggerResponse(204, "Prize-email re-send triggered")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> ResendPrizeEmailsAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new SendPrizeNotificationsCommand(roundId, Force: true), cancellationToken);

        return NoContent();
    }

    [HttpPost("{roundId:int}/reminders")]
    [SwaggerOperation(
        Summary = "Send ad-hoc prediction reminders",
        Description = "Emails a \"you are missing predictions\" reminder to the given players for the round. Players reminded within the throttle window, or who no longer have any missing fixtures, are skipped. Refused once the deadline has passed.")]
    [SwaggerResponse(200, "Reminders processed", typeof(SendPredictionRemindersResultDto))]
    [SwaggerResponse(400, "Deadline has passed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<ActionResult<SendPredictionRemindersResultDto>> SendRemindersAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        [FromBody, SwaggerParameter("Players to remind", Required = true)] SendPredictionRemindersRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendPredictionRemindersCommand(roundId, LeagueId: null, request.UserIds, CurrentUserId, IsSiteAdmin: true);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    #endregion
}
