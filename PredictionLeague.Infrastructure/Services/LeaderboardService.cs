using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Core.Services.DTOs;

namespace PredictionLeague.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly IGameWeekResultRepository _gameWeekResultRepository;

    private readonly ILeagueRepository _leagueRepository;
    // In a real app, you would have a user repository to get user names
    // private readonly IUserRepository _userRepository;

    public LeaderboardService(IGameWeekResultRepository gameWeekResultRepository, ILeagueRepository leagueRepository)
    {
        _gameWeekResultRepository = gameWeekResultRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetGameWeekLeaderboardAsync(int gameWeekId, int? leagueId = null)
    {
        var results = await _gameWeekResultRepository.GetByGameWeekIdAsync(gameWeekId);

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
        // This would require a more complex repository method to fetch gameweeks within a month,
        // then aggregate the results.
        throw new System.NotImplementedException();
    }

    public Task<IEnumerable<LeaderboardEntry>> GetYearlyLeaderboardAsync(int gameYearId, int? leagueId = null)
    {
        // This would require a more complex repository method to fetch all gameweeks for a year,
        // then aggregate the results.
        throw new System.NotImplementedException();
    }
}