using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Teams.Commands;
using PredictionLeague.Application.Features.Admin.Teams.Queries;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.API.Controllers;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/[controller]")]
public class TeamsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTeamAsync([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTeamCommand(
            request.Name,
            request.LogoUrl,
            request.Abbreviation
        );

        var createdTeam = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetTeamById", new { teamId = createdTeam.Id }, createdTeam);
    }

    #endregion

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamDto>>> FetchAllTeamsAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllTeamsQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{teamId:int}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetTeamByIdAsync(int teamId, CancellationToken cancellationToken)
    {
        var query = new GetTeamByIdQuery(teamId);
        var team = await _mediator.Send(query, cancellationToken);
       
        if (team == null)
            return NotFound();
        
        return Ok(team);
    }

    #endregion

    #region Update

    [HttpPut("{teamId:int}/update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTeamAsync(int teamId, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTeamCommand(
            teamId,
            request.Name,
            request.LogoUrl,
            request.Abbreviation
        );
        
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}