using PredictionLeague.Shared.Leaderboards;

namespace PredictionLeague.Application.Services;

public interface ILeaderboardService
{
    Task<IEnumerable<LeaderboardEntry>> GetRoundLeaderboardAsync(int roundId, int? leagueId = null);
    Task<IEnumerable<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int year, int month, int? leagueId = null);
    Task<IEnumerable<LeaderboardEntry>> GetYearlyLeaderboardAsync(int seasonId, int? leagueId = null);
}