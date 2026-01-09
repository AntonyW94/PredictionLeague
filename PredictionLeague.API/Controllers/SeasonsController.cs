using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Queries;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.API.Controllers;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/[controller]")]

public class SeasonsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public SeasonsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create 

    [HttpPost("create")]
    [ProducesResponseType(typeof(SeasonDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSeasonAsync([FromBody] CreateSeasonRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateSeasonCommand(
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            CurrentUserId,
            request.IsActive,
            request.NumberOfRounds,
            request.ApiLeagueId
        );

        var newSeasonDto = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetById", new { seasonId = newSeasonDto.Id }, newSeasonDto);
    }

    #endregion

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SeasonDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllSeasonsQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{seasonId:int}")]
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> GetByIdAsync(int seasonId, CancellationToken cancellationToken)
    {
        var query = new GetSeasonByIdQuery(seasonId);
        var season = await _mediator.Send(query, cancellationToken);

        if (season == null)
            return NotFound();

        return Ok(season);
    }

    #endregion

    #region Update

    [HttpPut("{seasonId:int}/update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSeasonAsync(int seasonId, [FromBody] UpdateSeasonRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonCommand(
            seasonId,
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsActive,
            request.NumberOfRounds,
            request.ApiLeagueId);

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{seasonId:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatusAsync(int seasonId, [FromBody] bool isActive, CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonStatusCommand(seasonId, isActive);
        await _mediator.Send(command, cancellationToken);
      
        return NoContent();
    }

    #endregion
}