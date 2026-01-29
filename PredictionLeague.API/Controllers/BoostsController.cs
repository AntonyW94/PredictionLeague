using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Contracts.Boosts;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Boosts - Apply score multipliers to predictions")]
public class BoostsController : ApiControllerBase
{
    private readonly IBoostService _boostService;
    private readonly IMediator _mediator;

    public BoostsController(IBoostService boostService, IMediator mediator)
    {
        _boostService = boostService;
        _mediator = mediator;
    }

    [HttpGet("available")]
    [SwaggerOperation(
        Summary = "Get available boosts",
        Description = "Returns boosts available to the user for the specified league and round. Includes remaining usage counts and eligibility status.")]
    [SwaggerResponse(200, "Available boosts retrieved successfully", typeof(IEnumerable<AvailableBoostDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery, SwaggerParameter("League identifier", Required = true)] int leagueId,
        [FromQuery, SwaggerParameter("Round identifier", Required = true)] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetAvailableBoostsQuery(leagueId, roundId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpPost("apply")]
    [SwaggerOperation(
        Summary = "Apply a boost to predictions",
        Description = "Applies a boost (e.g., Double Up) to the user's predictions for a specific league and round. The boost multiplies points earned.")]
    [SwaggerResponse(200, "Boost applied successfully", typeof(ApplyBoostResultDto))]
    [SwaggerResponse(400, "Boost not available or already used")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<ApplyBoostResultDto>> Apply(
        [FromBody, SwaggerParameter("Boost application details", Required = true)] ApplyBoostRequest req,
        CancellationToken cancellationToken)
    {
        var result = await _boostService.ApplyBoostAsync(CurrentUserId, req.LeagueId, req.RoundId, req.BoostCode, cancellationToken);
        if (result.Success)
            return Ok(result);

        return BadRequest(result);
    }

    [HttpDelete("user/usage")]
    [SwaggerOperation(
        Summary = "Remove a boost from predictions",
        Description = "Removes a previously applied boost from the user's predictions for a specific league and round. The boost usage is restored.")]
    [SwaggerResponse(204, "Boost removed successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> DeleteUserBoostUsage(
        [FromQuery, SwaggerParameter("League identifier", Required = true)] int leagueId,
        [FromQuery, SwaggerParameter("Round identifier", Required = true)] int roundId,
        CancellationToken cancellationToken)
    {
        await _boostService.DeleteUserBoostUsageAsync(CurrentUserId, leagueId, roundId, cancellationToken);

        return NoContent();
    }
}
