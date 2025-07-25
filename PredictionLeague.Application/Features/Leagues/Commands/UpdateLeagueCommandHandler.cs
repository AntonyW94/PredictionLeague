using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueCommandHandler : IRequestHandler<UpdateLeagueCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public UpdateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, league, "League");

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        league.UpdateDetails(
            request.Name,
            request.Price,
            request.EntryDeadline,
            season
        );
        
        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}