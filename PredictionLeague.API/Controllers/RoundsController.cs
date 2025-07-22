using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.API.Controllers;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/[controller]")]
public class RoundsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoundAsync([FromBody] CreateRoundRequest request)
    {
        var command = new CreateRoundCommand(
            request.SeasonId,
            request.RoundNumber,
            request.StartDate,
            request.Deadline,
            request.Matches
        );

        await _mediator.Send(command);

        return Ok(new { message = "Round and matches created successfully." });
    }

    #endregion

    #region Read

    [HttpGet("by-season/{seasonId:int}")]
    public async Task<IActionResult> FetchRoundsForSeasonAsync(int seasonId)
    {
        var query = new FetchRoundsForSeasonQuery(seasonId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{roundId:int}")]
    public async Task<IActionResult> GetRoundByIdAsync(int roundId)
    {
        var query = new GetRoundByIdQuery(roundId);
        var roundDetails = await _mediator.Send(query);

        return roundDetails == null ? NotFound() : Ok(roundDetails);
    }
    
    #endregion

    #region Update

    [HttpPut("{roundId:int}/update")]
    public async Task<IActionResult> UpdateRoundAsync(int roundId, [FromBody] UpdateRoundRequest request)
    {
        var command = new UpdateRoundCommand(
            roundId, 
            request.RoundNumber,
            request.StartDate, 
            request.Deadline, 
            request.Matches);

        await _mediator.Send(command);

        return Ok(new { message = "Round updated successfully." });
    }

    [HttpPut("{roundId:int}/submit-results")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubmitResultsAsync(int roundId, [FromBody] List<MatchResultDto> matches)
    {
        var command = new UpdateMatchResultsCommand(roundId, matches);

        await _mediator.Send(command);
        return NoContent();
    }
    
    #endregion
}