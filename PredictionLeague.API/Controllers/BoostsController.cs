using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Contracts.Boosts;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BoostsController : ControllerBase
{
    private readonly IBoostService _boostService;
    private readonly IMediator _mediator;

    public BoostsController(IBoostService boostService, IMediator mediator)
    {
        _boostService = boostService;
        _mediator = mediator;
    }

    [HttpGet("available")]
    [Authorize]
    public async Task<IActionResult> GetAvailable(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var query = new GetAvailableBoostsQuery(leagueId, roundId, userId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<ActionResult<ApplyBoostResultDto>> Apply([FromBody] ApplyBoostRequest req, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _boostService.ApplyBoostAsync(userId, req.LeagueId, req.RoundId, req.BoostCode, cancellationToken);
        if (result.Success)
            return Ok(result);

        return BadRequest(result);
    }

    [HttpDelete("user/usage")]
    [Authorize]
    public async Task<IActionResult> DeleteUserBoostUsage([FromQuery] int leagueId, [FromQuery] int roundId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _boostService.DeleteUserBoostUsageAsync(userId, leagueId, roundId, cancellationToken);

        return NoContent();
    }
}