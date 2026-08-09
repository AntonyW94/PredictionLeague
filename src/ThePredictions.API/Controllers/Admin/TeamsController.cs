using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.Teams.Commands;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Contracts.Admin.Teams;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Teams - Manage football teams (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class TeamsController(IMediator mediator) : ApiControllerBase
{
    #region Create

    [HttpPost("create")]
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

        var createdTeam = await mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetTeamById", new { teamId = createdTeam.Id }, createdTeam);
    }

    #endregion

    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all teams",
        Description = "Returns all football teams in the system. Optionally filter by season to show only teams with matches in that season.")]
    [SwaggerResponse(200, "Teams retrieved successfully", typeof(IEnumerable<TeamDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<TeamDto>>> FetchAllTeamsAsync(
        [FromQuery, SwaggerParameter("Optional season ID to filter teams")] int? seasonId,
        CancellationToken cancellationToken)
    {
        var query = new FetchAllTeamsQuery(seasonId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{teamId:int}")]
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
        return Ok(await mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Update

    [HttpPut("{teamId:int}/update")]
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

        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
