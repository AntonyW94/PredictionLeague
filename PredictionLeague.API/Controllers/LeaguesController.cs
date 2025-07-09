using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Leagues;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaguesController : ControllerBase
{
    private readonly ILeagueService _leagueService;

    public LeaguesController(ILeagueService leagueService)
    {
        _leagueService = leagueService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var newLeague = await _leagueService.CreateAsync(request, userId);
        
        return CreatedAtAction(nameof(GetLeagueById), new { leagueId = newLeague.Id }, newLeague);
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinLeague([FromBody] JoinLeagueRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _leagueService.JoinLeagueAsync(request.EntryCode, userId);
        
        return Ok(new { message = "Successfully joined league." });
    }

    [HttpPost("{leagueId:int}/join")]
    public async Task<IActionResult> JoinPublicLeague(int leagueId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _leagueService.JoinPublicLeagueAsync(leagueId, userId);
        
        return Ok(new { message = "Successfully joined league." });
    }

    [HttpGet("{leagueId:int}")]
    public IActionResult GetLeagueById(int leagueId)
    {
        // Note: Implementation for this would require a new GetByIdAsync method in ILeagueService
        // and ILeagueRepository. For now, it serves as a target for CreatedAtAction.
        return Ok(new { message = $"Endpoint to get league {leagueId}." });
    }
}