using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Queries;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]

public class SeasonsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public SeasonsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create 

    [HttpPost("create")]
    public async Task<IActionResult> CreateSeasonAsync([FromBody] CreateSeasonRequest request)
    {
        var command = new CreateSeasonCommand(
            request.Name,
            request.StartDate,
            request.EndDate,
            CurrentUserId,
            request.IsActive
        );

        var newSeasonDto = await _mediator.Send(command);
        return CreatedAtAction("GetById", new { seasonId = newSeasonDto.Id }, newSeasonDto);
    }

    #endregion

    #region Read

    [HttpGet]
    public async Task<IActionResult> FetchAllAsync()
    {
        var query = new FetchAllSeasonsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{seasonId:int}")]
    public async Task<IActionResult> GetByIdAsync(int seasonId)
    {
        var query = new GetSeasonByIdQuery(seasonId);

        var season = await _mediator.Send(query);
        return season == null ? NotFound() : Ok(season);
    }

    #endregion

    #region Update

    [HttpPut("{seasonId:int}/update")]
    public async Task<IActionResult> UpdateSeasonAsync(int seasonId, [FromBody] UpdateSeasonRequest request)
    {
        var command = new UpdateSeasonCommand(
            seasonId,
            request.Name,
            request.StartDate,
            request.EndDate,
            request.IsActive);

        await _mediator.Send(command);
        return Ok(new { message = "Season updated successfully." });
    }

    #endregion
}