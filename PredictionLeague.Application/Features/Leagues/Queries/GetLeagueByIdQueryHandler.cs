using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueByIdQueryHandler : IRequestHandler<GetLeagueByIdQuery, LeagueDto?>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public GetLeagueByIdQueryHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.Id);
        if (league == null)
            return null;

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId);
        if (season == null)
            return null;

        return new LeagueDto
        {
            Id = league.Id,
            Name = league.Name,
            SeasonName = season.Name,
            MemberCount = (await _leagueRepository.GetMembersByLeagueIdAsync(league.Id)).Count(),
            EntryCode = league.EntryCode ?? "Public"
        };
    }
}