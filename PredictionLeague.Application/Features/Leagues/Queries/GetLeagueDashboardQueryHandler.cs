using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueDashboardQueryHandler : IRequestHandler<GetLeagueDashboardQuery, LeagueDashboardDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUserPredictionRepository _predictionRepository;

    public GetLeagueDashboardQueryHandler(
        ILeagueRepository leagueRepository,
        ISeasonRepository seasonRepository,
        IUserPredictionRepository predictionRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _predictionRepository = predictionRepository;
    }

    public async Task<LeagueDashboardDto> Handle(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId) ?? throw new KeyNotFoundException("League not found.");
        var season = await _seasonRepository.GetByIdAsync(league.SeasonId) ?? throw new KeyNotFoundException("Season not found.");

        IEnumerable<LeaderboardEntryDto> entries;

        if (request.Month.HasValue)
            entries = await _predictionRepository.GetMonthlyLeaderboardAsync(request.LeagueId, request.Month.Value);
        else
            entries = await _predictionRepository.GetOverallLeaderboardAsync(request.LeagueId);

        return new LeagueDashboardDto
        {
            LeagueName = league.Name,
            SeasonName = season.Name,
            Entries = entries.ToList()
        };
    }
}
