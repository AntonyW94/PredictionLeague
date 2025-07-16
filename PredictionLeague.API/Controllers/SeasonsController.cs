using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Queries;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]

public class SeasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeasonsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create 

    [HttpPost("create")]
    public async Task<IActionResult> CreateSeason([FromBody] CreateSeasonRequest request)
    {
        var command = new CreateSeasonCommand
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await _mediator.Send(command);

        return Ok(new { message = "Season created successfully." });
    }

    #endregion

    #region Read

    [HttpGet]
    public async Task<IActionResult> FetchAll()
    {
        var query = new FetchAllSeasonsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var query = new GetSeasonByIdQuery { Id = id };

        var season = await _mediator.Send(query);
        return season == null ? NotFound() : Ok(season);
    }

    #endregion

    #region Update

    [HttpPut("{id:int}/update")]
    public async Task<IActionResult> UpdateSeason(int id, [FromBody] UpdateSeasonRequest request)
    {
        var command = new UpdateSeasonCommand
        {
            Id = id,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };

        await _mediator.Send(command);
        return Ok(new { message = "Season updated successfully." });
    }

    #endregion
}