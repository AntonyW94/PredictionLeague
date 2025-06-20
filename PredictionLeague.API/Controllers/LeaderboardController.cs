using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Services;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        // GET: api/leaderboard/gameweek/5
        // GET: api/leaderboard/gameweek/5?leagueId=10
        [HttpGet("gameweek/{gameWeekId}")]
        public async Task<IActionResult> GetGameWeekLeaderboard(int gameWeekId, [FromQuery] int? leagueId)
        {
            var leaderboard = await _leaderboardService.GetGameWeekLeaderboardAsync(gameWeekId, leagueId);
            return Ok(leaderboard);
        }

        // Placeholder for monthly leaderboard
        // GET: api/leaderboard/monthly/2024/8?leagueId=10
        [HttpGet("monthly/{year}/{month}")]
        public async Task<IActionResult> GetMonthlyLeaderboard(int year, int month, [FromQuery] int? leagueId)
        {
            var leaderboard = await _leaderboardService.GetMonthlyLeaderboardAsync(year, month, leagueId);
            return Ok(leaderboard);
        }

        // Placeholder for yearly leaderboard
        // GET: api/leaderboard/yearly/3?leagueId=10
        [HttpGet("yearly/{gameYearId}")]
        public async Task<IActionResult> GetYearlyLeaderboard(int gameYearId, [FromQuery] int? leagueId)
        {
            var leaderboard = await _leaderboardService.GetYearlyLeaderboardAsync(gameYearId, leagueId);
            return Ok(leaderboard);
        }
    }
}
