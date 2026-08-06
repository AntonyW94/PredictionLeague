using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.Competitions.Commands;
using ThePredictions.Application.Features.Admin.Competitions.Queries;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Competitions - Manage competition reference data (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class CompetitionsController(IMediator mediator) : ApiControllerBase
{
    #region Create

    [HttpPost("create")]
    [SwaggerOperation(
        Summary = "Create a new competition",
        Description = "Creates a competition (the stable, provider-independent identity that seasons belong to).")]
    [SwaggerResponse(201, "Competition created successfully", typeof(CompetitionDto))]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> CreateCompetitionAsync(
        [FromBody, SwaggerParameter("Competition details", Required = true)] CreateCompetitionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCompetitionCommand(
            request.Code,
            request.Name,
            (CompetitionType)request.Type,
            request.LogoUrl,
            request.Description,
            request.ApiLeagueId
        );

        var createdCompetition = await mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetCompetitionById", new { competitionId = createdCompetition.Id }, createdCompetition);
    }

    #endregion

    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all competitions",
        Description = "Returns all competitions in the system.")]
    [SwaggerResponse(200, "Competitions retrieved successfully", typeof(IEnumerable<CompetitionDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<CompetitionDto>>> FetchAllCompetitionsAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllCompetitionsQuery();
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{competitionId:int}")]
    [SwaggerOperation(
        Summary = "Get competition by ID",
        Description = "Returns details of a specific competition including logo URL and API league id.")]
    [SwaggerResponse(200, "Competition retrieved successfully", typeof(CompetitionDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Competition not found")]
    public async Task<ActionResult<CompetitionDto>> GetCompetitionByIdAsync(
        [SwaggerParameter("Competition identifier")] int competitionId,
        CancellationToken cancellationToken)
    {
        var query = new GetCompetitionByIdQuery(competitionId);
        var competition = await mediator.Send(query, cancellationToken);

        if (competition == null)
            return NotFound();

        return Ok(competition);
    }

    #endregion

    #region Update

    [HttpPut("{competitionId:int}/update")]
    [SwaggerOperation(
        Summary = "Update competition details",
        Description = "Updates a competition's code, name, type, logo, and API league id. Repointing the API league id needs no deploy.")]
    [SwaggerResponse(204, "Competition updated successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Competition not found")]
    public async Task<IActionResult> UpdateCompetitionAsync(
        [SwaggerParameter("Competition identifier")] int competitionId,
        [FromBody, SwaggerParameter("Updated competition details", Required = true)] UpdateCompetitionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCompetitionCommand(
            competitionId,
            request.Code,
            request.Name,
            (CompetitionType)request.Type,
            request.LogoUrl,
            request.Description,
            request.ApiLeagueId
        );

        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Delete

    [HttpDelete("{competitionId:int}")]
    [SwaggerOperation(
        Summary = "Delete a competition",
        Description = "Permanently deletes a competition. Competitions with seasons cannot be deleted.")]
    [SwaggerResponse(204, "Competition deleted successfully")]
    [SwaggerResponse(400, "Cannot delete - competition has seasons")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Competition not found")]
    public async Task<IActionResult> DeleteCompetitionAsync(
        [SwaggerParameter("Competition identifier")] int competitionId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCompetitionCommand(competitionId);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
