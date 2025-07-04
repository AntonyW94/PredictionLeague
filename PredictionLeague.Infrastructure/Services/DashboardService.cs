using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Dashboard;

namespace PredictionLeague.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public DashboardService(
        IRoundRepository roundRepository,
        IMatchRepository matchRepository,
        IUserPredictionRepository predictionRepository,
        ILeagueRepository leagueRepository,
        ISeasonRepository seasonRepository)
    {
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
        _predictionRepository = predictionRepository;
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<DashboardDto> GetDashboardDataAsync(string userId)
    {
        var userLeagues = (await _leagueRepository.GetLeaguesByUserIdAsync(userId)).ToList();
        var seasonIds = userLeagues.Select(l => l.SeasonId).Distinct();
        var upcomingRounds = new List<UpcomingRoundDto>();

        foreach (var seasonId in seasonIds)
        {
            var currentRound = await _roundRepository.GetCurrentRoundAsync(seasonId);
            if (currentRound == null) 
                continue;
            
            var season = await _seasonRepository.GetByIdAsync(seasonId);
            var matches = await _matchRepository.GetByRoundIdAsync(currentRound.Id);
            var userPredictions = await _predictionRepository.GetByUserIdAndRoundIdAsync(userId, currentRound.Id);

            upcomingRounds.Add(new UpcomingRoundDto
            {
                Id = currentRound.Id,
                SeasonName = season?.Name ?? "Unknown Season",
                RoundNumber = currentRound.RoundNumber,
                Deadline = currentRound.Deadline,
                Matches = matches.Select(m =>
                {
                    var prediction = userPredictions.FirstOrDefault(p => p.MatchId == m.Id);
                    return new MatchPredictionDto
                    {
                        MatchId = m.Id,
                        MatchDateTime = m.MatchDateTime,
                        HomeTeamName = m.HomeTeam!.Name,
                        HomeTeamLogoUrl = m.HomeTeam.LogoUrl!,
                        AwayTeamName = m.AwayTeam!.Name,
                        AwayTeamLogoUrl = m.AwayTeam.LogoUrl!,
                        PredictedHomeScore = prediction?.PredictedHomeScore,
                        PredictedAwayScore = prediction?.PredictedAwayScore
                    };
                }).ToList()
            });
        }

        var allPublicLeagues = await _leagueRepository.GetPublicLeaguesAsync();
        var userLeagueIds = userLeagues.Select(l => l.Id).ToHashSet();
        var publicLeagues = new List<PublicLeagueDto>();
       
        foreach (var league in allPublicLeagues)
        {
            var season = await _seasonRepository.GetByIdAsync(league.SeasonId);
            
            publicLeagues.Add(new PublicLeagueDto
            {
                Id = league.Id,
                Name = league.Name,
                SeasonName = season?.Name ?? "N/A",
                IsMember = userLeagueIds.Contains(league.Id)
            });
        }
            
        return new DashboardDto
        {
            UpcomingRounds = upcomingRounds,
            PublicLeagues = publicLeagues.ToList()
        };
    }
}