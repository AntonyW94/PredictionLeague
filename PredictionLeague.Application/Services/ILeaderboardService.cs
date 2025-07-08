using PredictionLeague.Shared.Leaderboards;

namespace PredictionLeague.Application.Services;

public interface ILeaderboardService
{
    Task<LeaderboardDto> GetOverallLeaderboardAsync(int leagueId);
    Task<LeaderboardDto> GetMonthlyLeaderboardAsync(int leagueId, int month, int year);
}