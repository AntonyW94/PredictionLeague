using PredictionLeague.Core.Services.DTOs;

namespace PredictionLeague.Core.Services;

public interface ILeaderboardService
{
    Task<IEnumerable<LeaderboardEntry>> GetGameWeekLeaderboardAsync(int gameWeekId, int? leagueId = null);
    Task<IEnumerable<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int year, int month, int? leagueId = null);
    Task<IEnumerable<LeaderboardEntry>> GetYearlyLeaderboardAsync(int gameYearId, int? leagueId = null);
}