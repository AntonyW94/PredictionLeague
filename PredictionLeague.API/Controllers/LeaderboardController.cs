using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]/{leagueId:int}")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("overall")]
    public async Task<IActionResult> GetOverallLeaderboard(int leagueId)
    {
        return Ok(await _leaderboardService.GetOverallLeaderboardAsync(leagueId));
    }
    
    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthlyLeaderboard(int leagueId, [FromQuery] int month, [FromQuery] int year)
    {
        return Ok(await _leaderboardService.GetMonthlyLeaderboardAsync(leagueId, month, year));
    }
}