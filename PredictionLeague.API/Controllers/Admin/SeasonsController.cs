using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Queries;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Seasons - Manage competition seasons (Admin only)")]
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
    [SwaggerOperation(
        Summary = "Create a new season",
        Description = "Creates a new competition season with the specified configuration. Links to external football API for fixture data.")]
    [SwaggerResponse(201, "Season created successfully", typeof(SeasonDto))]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> CreateSeasonAsync(
        [FromBody, SwaggerParameter("Season configuration", Required = true)] CreateSeasonRequest request,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Get all seasons",
        Description = "Returns all seasons in the system, both active and inactive.")]
    [SwaggerResponse(200, "Seasons retrieved successfully", typeof(IEnumerable<SeasonDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllSeasonsQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{seasonId:int}")]
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get season by ID",
        Description = "Returns details of a specific season including configuration and linked API league.")]
    [SwaggerResponse(200, "Season retrieved successfully", typeof(SeasonDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<ActionResult<SeasonDto>> GetByIdAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Update season details",
        Description = "Updates an existing season's configuration including name, dates, and API linkage.")]
    [SwaggerResponse(204, "Season updated successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<IActionResult> UpdateSeasonAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        [FromBody, SwaggerParameter("Updated season configuration", Required = true)] UpdateSeasonRequest request,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Update season active status",
        Description = "Activates or deactivates a season. Inactive seasons are hidden from users.")]
    [SwaggerResponse(204, "Status updated successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<IActionResult> UpdateStatusAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        [FromBody, SwaggerParameter("New active status", Required = true)] bool isActive,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonStatusCommand(seasonId, isActive);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
