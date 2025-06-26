using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Leaderboards;

namespace PredictionLeague.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly IRoundResultRepository _roundResultRepository;

    private readonly ILeagueRepository _leagueRepository;
    // In a real app, you would have a user repository to get user names
    // private readonly IUserRepository _userRepository;

    public LeaderboardService(IRoundResultRepository roundResultRepository, ILeagueRepository leagueRepository)
    {
        _roundResultRepository = roundResultRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetRoundLeaderboardAsync(int roundId, int? leagueId = null)
    {
        var results = await _roundResultRepository.GetByRoundIdAsync(roundId);

        if (leagueId.HasValue)
        {
            var leagueMembers = await _leagueRepository.GetMembersByLeagueIdAsync(leagueId.Value);
            var memberIds = leagueMembers.Select(m => m.UserId).ToHashSet();
            results = results.Where(r => memberIds.Contains(r.UserId));
        }

        var leaderboard = results
            .OrderByDescending(r => r.TotalPoints)
            .Select((r, index) => new LeaderboardEntry
            {
                Rank = index + 1,
                UserId = r.UserId,
                UserName = "User " + r.UserId.Substring(0, 5), // Placeholder for username
                TotalPoints = r.TotalPoints
            }).ToList();

        return leaderboard;
    }

    public Task<IEnumerable<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int year, int month, int? leagueId = null)
    {
        // This would require a more complex repository method to fetch rounds within a month,
        // then aggregate the results.
        throw new System.NotImplementedException();
    }

    public Task<IEnumerable<LeaderboardEntry>> GetYearlyLeaderboardAsync(int seasonId, int? leagueId = null)
    {
        // This would require a more complex repository method to fetch all rounds for a year,
        // then aggregate the results.
        throw new System.NotImplementedException();
    }
}