using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Web.Client.Services.Leagues;

public interface ILeagueService
{
    Task<(bool Success, string? ErrorMessage)> JoinPublicLeagueAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> JoinPrivateLeagueAsync(string entryCode);
    Task<List<MyLeagueDto>> GetMyLeaguesAsync();
    Task<List<AvailableLeagueDto>> GetAvailableLeaguesAsync();
    Task<(bool Success, string? ErrorMessage)> RemoveMyLeagueMembershipAsync(int leagueId);

    Task<IEnumerable<LeaderboardEntryDto>> GetOverallLeaderboard(int leagueId);
}