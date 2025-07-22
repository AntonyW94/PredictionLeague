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
        var leagueToUpdate = await _leagueRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new KeyNotFoundException($"League with ID {request.Id} not found.");

        leagueToUpdate.Name = request.Name;
        leagueToUpdate.EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode;

        await _leagueRepository.UpdateAsync(leagueToUpdate, cancellationToken);
    }
}