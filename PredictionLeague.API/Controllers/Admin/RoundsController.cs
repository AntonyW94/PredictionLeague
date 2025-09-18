using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
public class RoundsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    [ProducesResponseType(typeof(RoundDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRoundAsync([FromBody] CreateRoundRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateRoundCommand(
            request.SeasonId,
            request.RoundNumber,
            request.ApiRoundName,
            request.StartDate,
            request.Deadline,
            request.Matches
        );

        var newRound = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetRoundById", new { roundId = newRound.Id }, newRound);
    }

    #endregion

    #region Read

    [HttpGet("by-season/{seasonId:int}")]
    [ProducesResponseType(typeof(IEnumerable<RoundWithAllPredictionsInDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoundWithAllPredictionsInDto>>> FetchRoundsForSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        var query = new FetchRoundsForSeasonQuery(seasonId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{roundId:int}")]
    [ProducesResponseType(typeof(RoundDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoundDetailsDto>> GetRoundByIdAsync(int roundId, CancellationToken cancellationToken)
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoundAsync(int roundId, [FromBody] UpdateRoundRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoundCommand(
            roundId,
            request.RoundNumber,
            request.ApiRoundName,
            request.StartDate,
            request.Deadline,
            request.Status,
            request.Matches);

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{roundId:int}/results")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitResultsAsync(int roundId, [FromBody] List<MatchResultDto> matches, CancellationToken cancellationToken)
    {
        var command = new UpdateMatchResultsCommand(roundId, matches);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{roundId:int}/send-prediction-reminder-emails")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChaseEmails(int roundId, CancellationToken cancellationToken)
    {
        var command = new SendPredictionsMissingEmailsCommand(roundId);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}