using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Web.Client.Services.Leagues;

public interface ILeagueService
{
    Task<List<MyLeagueDto>> GetMyLeaguesAsync();
    Task<List<AvailableLeagueDto>> GetAvailableLeaguesAsync();
    Task<List<LeagueLeaderboardDto>> GetLeaderboardsAsync();
    Task<List<UpcomingRoundDto>> GetUpcomingRoundsAsync();
    Task<List<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId);
    Task<List<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month);
    Task<ExactScoresLeaderboardDto> GetExactScoresLeaderboardAsync(int leagueId);
    Task<List<MonthDto>> GetMonthsForLeagueAsync(int leagueId);
    Task<WinningsDto> GetWinningsAsync(int leagueId);

    Task<(bool Success, string? ErrorMessage)> RemoveMyLeagueMembershipAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> JoinPublicLeagueAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> JoinPrivateLeagueAsync(string entryCode);
}