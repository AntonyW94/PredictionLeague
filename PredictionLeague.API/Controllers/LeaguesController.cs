using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Leagues;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // All actions in this controller require an authenticated user.
public class LeaguesController : ControllerBase
{
    private readonly ILeagueService _leagueService;

    public LeaguesController(ILeagueService leagueService)
    {
        _leagueService = leagueService;
    }

    // POST: api/leagues
    [HttpPost]
    public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueRequest request)
    {
        // Get the user's ID from the JWT token claims.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var newLeague = await _leagueService.CreateAsync(request, userId);
            // Return a 201 Created status with the location of the new resource and the created object.
            return CreatedAtAction(nameof(GetLeagueById), new { leagueId = newLeague.Id }, newLeague);
        }
        catch (Exception ex)
        {
            // In a real app, log the exception
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/leagues/join (for private leagues)
    [HttpPost("join")]
    public async Task<IActionResult> JoinLeague([FromBody] JoinLeagueRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _leagueService.JoinLeagueAsync(request.EntryCode, userId);
            return Ok(new { message = "Successfully joined league." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{leagueId:int}/join")]
    public async Task<IActionResult> JoinPublicLeague(int leagueId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _leagueService.JoinPublicLeagueAsync(leagueId, userId);
            return Ok(new { message = "Successfully joined league." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{leagueId:int}")]
    public IActionResult GetLeagueById(int leagueId)
    {
        // Note: Implementation for this would require a new GetByIdAsync method in ILeagueService
        // and ILeagueRepository. For now, it serves as a target for CreatedAtAction.
        return Ok(new { message = $"Endpoint to get league {leagueId}." });
    }
}