using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Queries;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

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
    public async Task<IActionResult> CreateSeasonAsync([FromBody] CreateSeasonRequest request)
    {
        var creatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(creatorId))
            return Unauthorized();
        
        var command = new CreateSeasonCommand(request, creatorId);
        await _mediator.Send(command);

        return Ok(new { message = "Season created successfully." });
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
        var command = new UpdateSeasonCommand(seasonId, request);

        await _mediator.Send(command);
        return Ok(new { message = "Season updated successfully." });
    }

    #endregion
}