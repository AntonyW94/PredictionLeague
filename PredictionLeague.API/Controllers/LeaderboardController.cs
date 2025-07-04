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

    // GET: api/leaderboard/round/5
    // GET: api/leaderboard/round/5?leagueId=10
    [HttpGet("round/{roundId:int}")]
    public async Task<IActionResult> GetRoundLeaderboard(int roundId, [FromQuery] int? leagueId)
    {
        var leaderboard = await _leaderboardService.GetRoundLeaderboardAsync(roundId, leagueId);
        return Ok(leaderboard);
    }

    // Placeholder for monthly leaderboard
    // GET: api/leaderboard/monthly/2024/8?leagueId=10
    [HttpGet("monthly/{year:int}/{month:int}")]
    public async Task<IActionResult> GetMonthlyLeaderboard(int year, int month, [FromQuery] int? leagueId)
    {
        var leaderboard = await _leaderboardService.GetMonthlyLeaderboardAsync(year, month, leagueId);
        return Ok(leaderboard);
    }

    // Placeholder for yearly leaderboard
    // GET: api/leaderboard/yearly/3?leagueId=10
    [HttpGet("yearly/{seasonId:int}")]
    public async Task<IActionResult> GetYearlyLeaderboard(int seasonId, [FromQuery] int? leagueId)
    {
        var leaderboard = await _leaderboardService.GetYearlyLeaderboardAsync(seasonId, leagueId);
        return Ok(leaderboard);
    }
}