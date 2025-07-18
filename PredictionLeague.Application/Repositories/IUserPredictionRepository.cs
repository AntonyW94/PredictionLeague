using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IUserPredictionRepository
{
    Task<IEnumerable<UserPrediction>> GetByMatchIdAsync(int matchId);
    Task<IEnumerable<UserPrediction>> GetByUserIdAndRoundIdAsync(string userId, int roundId);
    Task UpsertAsync(UserPrediction prediction);
    Task UpdatePointsAsync(int predictionId, int points);
    Task<IEnumerable<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId);
    Task<IEnumerable<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month);
    Task<IEnumerable<LeaderboardEntryDto>> GetRoundLeaderboardAsync(int leagueId, int roundId);
}