using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand>
{
    private readonly ITeamRepository _teamRepository;

    public UpdateTeamCommandHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, team, $"Team (ID: {request.Id}) not found.");

        team.UpdateDetails(request.Name, request.LogoUrl);

        await _teamRepository.UpdateAsync(team, cancellationToken);
    }
}