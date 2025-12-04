using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Contracts.Boosts;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BoostsController : ControllerBase
{
    private readonly IBoostService _boostService;

    public BoostsController(IBoostService boostService)
    {
        _boostService = boostService;
    }

    [HttpGet("eligibility")]
    [Authorize]
    public async Task<ActionResult<BoostEligibilityDto>> GetEligibility(
        [FromQuery] int leagueId,
        [FromQuery] int roundId,
        [FromQuery] string boostCode,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
        if (string.IsNullOrEmpty(userId)) 
            return Unauthorized();

        var dto = await _boostService.GetBoostEligibilityAsync(userId, leagueId, roundId, boostCode, cancellationToken);
        return Ok(dto);
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

    public class ApplyBoostRequest
    {
        public int LeagueId { get; set; }
        public int RoundId { get; set; }
        public string BoostCode { get; set; } = null!;
    }
}