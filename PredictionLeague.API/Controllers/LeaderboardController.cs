using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("{leagueId:int}/overall")]
    public async Task<IActionResult> GetOverallLeaderboard(int leagueId)
    {
        var leaderboard = await _leaderboardService.GetOverallLeaderboardAsync(leagueId);
        return Ok(leaderboard);
    }
    
    [HttpGet("{leagueId:int}/monthly")]
    public async Task<IActionResult> GetMonthlyLeaderboard(int leagueId, [FromQuery] int month, [FromQuery] int year)
    {
        var leaderboard = await _leaderboardService.GetMonthlyLeaderboardAsync(leagueId, month, year);
        return Ok(leaderboard);
    }
}