using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;

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
        Guard.Against.NotFound(request.Id, league, $"League (ID: {request.Id}) not found.");

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.NotFound(league.SeasonId, season, $"Season (ID: {league.SeasonId}) was not found.");

        league.UpdateDetails(
            request.Name,
            request.Price,
            request.EntryDeadline,
            season
        );
        
        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}