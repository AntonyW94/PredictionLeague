using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Leaderboards;

namespace PredictionLeague.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUserPredictionRepository _predictionRepository;

    public LeaderboardService(
        ILeagueRepository leagueRepository,
        ISeasonRepository seasonRepository,
        IUserPredictionRepository predictionRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _predictionRepository = predictionRepository;
    }

    public async Task<LeaderboardDto> GetOverallLeaderboardAsync(int leagueId)
    {
        var league = await _leagueRepository.GetByIdAsync(leagueId) ?? throw new KeyNotFoundException("League not found.");
        var season = await _seasonRepository.GetByIdAsync(league.SeasonId) ?? throw new KeyNotFoundException("Season not found.");
        var entries = await _predictionRepository.GetOverallLeaderboardAsync(leagueId);

        return new LeaderboardDto
        {
            LeagueName = league.Name,
            SeasonName = season.Name,
            Entries = entries.ToList()
        };
    }

    public async Task<LeaderboardDto> GetMonthlyLeaderboardAsync(int leagueId, int month, int year)
    {
        var league = await _leagueRepository.GetByIdAsync(leagueId) ?? throw new KeyNotFoundException("League not found.");
        var season = await _seasonRepository.GetByIdAsync(league.SeasonId) ?? throw new KeyNotFoundException("Season not found.");
        var entries = await _predictionRepository.GetMonthlyLeaderboardAsync(leagueId, month, year);

        return new LeaderboardDto
        {
            LeagueName = league.Name,
            SeasonName = season.Name,
            Entries = entries.ToList()
        };
    }
}