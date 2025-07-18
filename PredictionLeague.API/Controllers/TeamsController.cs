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
    public async Task<IActionResult> GetAllTeamsAsync()
    {
        var query = new FetchAllTeamsQuery();
        
        var result = await _mediator.Send(query);
        
        return Ok(result);
    }

    [HttpGet("{teamId:int}")]
    public async Task<IActionResult> GetTeamByIdAsync(int teamId)
    {
        var query = new GetTeamByIdQuery(teamId);

        var team = await _mediator.Send(query);
        
        return team == null ? NotFound() : Ok(team);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateTeamAsync([FromBody] CreateTeamRequest request)
    {
        var command = new CreateTeamCommand(request);

        var createdTeam = await _mediator.Send(command);
        
        return CreatedAtAction(nameof(GetTeamByIdAsync).Replace("Async", string.Empty), "Teams", new { id = createdTeam.Id }, createdTeam);
    }

    [HttpPut("{teamId:int}/update")]
    public async Task<IActionResult> UpdateTeamAsync(int teamId, [FromBody] UpdateTeamRequest request)
    {
        var command = new UpdateTeamCommand(teamId, request);

        await _mediator.Send(command);
        
        return Ok(new { message = "Team updated successfully." });
    }
}