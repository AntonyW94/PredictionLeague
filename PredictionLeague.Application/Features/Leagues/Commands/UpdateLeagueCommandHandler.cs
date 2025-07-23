using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueCommandHandler : IRequestHandler<UpdateLeagueCommand>
{
    private readonly ILeagueRepository _leagueRepository;

    public UpdateLeagueCommandHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.Id, cancellationToken);
       
        Guard.Against.NotFound(request.Id, league, $"League (ID: {request.Id}) not found.");
      
        league.UpdateDetails(
            request.Name,
            request.EntryCode,
            request.EntryDeadline
        );
        
        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}