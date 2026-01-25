using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards;

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

        if (league.AdministratorUserId != request.UserId)
            throw new UnauthorizedAccessException("Only the league administrator can update the league.");

        if (league.EntryDeadlineUtc < DateTime.UtcNow)
            throw new InvalidOperationException("This league cannot be edited because its entry deadline has passed.");
      
        if (league.Price != request.Price && league.Members.Count > 1)
            throw new InvalidOperationException("The entry fee cannot be changed after other players have joined the league.");

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");
        
        league.UpdateDetails(
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            season
        );
        
        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}