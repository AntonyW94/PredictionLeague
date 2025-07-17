using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchAllLeaguesQueryHandler : IRequestHandler<FetchAllLeaguesQuery, IEnumerable<LeagueDto>>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public FetchAllLeaguesQueryHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<IEnumerable<LeagueDto>> Handle(FetchAllLeaguesQuery request, CancellationToken cancellationToken)
    {
        var leagues = await _leagueRepository.GetAllAsync();
        var leaguesToReturn = new List<LeagueDto>();

        foreach (var league in leagues)
        {
            var season = await _seasonRepository.GetByIdAsync(league.SeasonId);
            if (season == null)
            {
                //Add error logging here
                continue;
            }

            leaguesToReturn.Add(new LeagueDto
            {
                Id = league.Id,
                Name = league.Name,
                SeasonName = season.Name,
                MemberCount = (await _leagueRepository.GetMembersByLeagueIdAsync(league.Id)).Count(),
                EntryCode = league.EntryCode ?? "Public"
            });
        }

        return leaguesToReturn;
    }
}