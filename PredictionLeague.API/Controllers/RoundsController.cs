using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Rounds.Queries;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.API.Controllers;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/[controller]")]
public class RoundsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    public async Task<IActionResult> CreateRound([FromBody] CreateRoundRequest request)
    {
        var command = new CreateRoundCommand
        {
            SeasonId = request.SeasonId,
            RoundNumber = request.RoundNumber,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            Matches = request.Matches
        };

        await _mediator.Send(command);

        return Ok(new { message = "Round and matches created successfully." });
    }

    #endregion

    #region Read

    [HttpGet("by-season/{seasonId:int}")]
    public async Task<IActionResult> FetchRoundsForSeason(int seasonId)
    {
        var query = new FetchRoundsForSeasonQuery(seasonId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{roundId:int}")]
    public async Task<IActionResult> GetRoundById(int roundId)
    {
        var query = new GetRoundByIdQuery(roundId);
        var roundDetails = await _mediator.Send(query);

        return roundDetails == null ? NotFound() : Ok(roundDetails);
    }
    
    #endregion

    #region Update

    [HttpPut("{roundId:int}/update")]
    public async Task<IActionResult> UpdateRound(int roundId, [FromBody] UpdateRoundRequest request)
    {
        var command = new UpdateRoundCommand(roundId, request);

        await _mediator.Send(command);

        return Ok(new { message = "Round updated successfully." });
    }

    [HttpPut("{roundId:int}/submit-results")]
    public async Task<IActionResult> SubmitResults(int roundId, [FromBody] List<UpdateMatchResultsRequest> request)
    {
        var command = new UpdateMatchResultsCommand(roundId, request);
        await _mediator.Send(command);

        return Ok(new { message = "Results updated and points calculated successfully." });
    }
    
    #endregion
}