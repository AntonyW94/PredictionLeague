using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Teams.Commands;
using PredictionLeague.Application.Features.Admin.Teams.Queries;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTeams()
    {
        var query = new FetchAllTeamsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTeamById(int id)
    {
        var query = new GetTeamByIdQuery { Id = id };

        var team = await _mediator.Send(query);
        return team == null ? NotFound() : Ok(team);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request)
    {
        var command = new CreateTeamCommand
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl
        };

        var createdTeam = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTeamById), "Teams", new { id = createdTeam.Id }, createdTeam);
    }

    [HttpPut("{id:int}/update")]
    public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTeamRequest request)
    {
        var command = new UpdateTeamCommand
        {
            Id = id,
            Name = request.Name,
            LogoUrl = request.LogoUrl
        };

        await _mediator.Send(command);
        return Ok(new { message = "Team updated successfully." });
    }
}