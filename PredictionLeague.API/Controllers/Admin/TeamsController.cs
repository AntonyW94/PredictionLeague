using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Teams.Commands;
using PredictionLeague.Application.Features.Admin.Teams.Queries;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Teams - Manage football teams (Admin only)")]
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
    [SwaggerOperation(
        Summary = "Create a new team",
        Description = "Creates a new football team with name, logo, and external API linkage.")]
    [SwaggerResponse(201, "Team created successfully", typeof(TeamDto))]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> CreateTeamAsync(
        [FromBody, SwaggerParameter("Team details", Required = true)] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTeamCommand(
            request.Name,
            request.ShortName,
            request.LogoUrl,
            request.Abbreviation,
            request.ApiTeamId
        );

        var createdTeam = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetTeamById", new { teamId = createdTeam.Id }, createdTeam);
    }

    #endregion

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get all teams",
        Description = "Returns all football teams in the system.")]
    [SwaggerResponse(200, "Teams retrieved successfully", typeof(IEnumerable<TeamDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<TeamDto>>> FetchAllTeamsAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllTeamsQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{teamId:int}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get team by ID",
        Description = "Returns details of a specific team including logo URL and API linkage.")]
    [SwaggerResponse(200, "Team retrieved successfully", typeof(TeamDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Team not found")]
    public async Task<ActionResult<TeamDto>> GetTeamByIdAsync(
        [SwaggerParameter("Team identifier")] int teamId,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Update team details",
        Description = "Updates an existing team's information including name, logo, and abbreviation.")]
    [SwaggerResponse(204, "Team updated successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Team not found")]
    public async Task<IActionResult> UpdateTeamAsync(
        [SwaggerParameter("Team identifier")] int teamId,
        [FromBody, SwaggerParameter("Updated team details", Required = true)] UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTeamCommand(
            teamId,
            request.Name,
            request.ShortName,
            request.LogoUrl,
            request.Abbreviation,
            request.ApiTeamId
        );

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
