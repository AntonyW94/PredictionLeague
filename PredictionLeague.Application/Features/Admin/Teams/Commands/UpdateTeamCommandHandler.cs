using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand>
{
    private readonly ILogger<UpdateTeamCommandHandler> _logger;
    private readonly ITeamRepository _teamRepository;

    public UpdateTeamCommandHandler(ILogger<UpdateTeamCommandHandler> logger, ITeamRepository teamRepository)
    {
        _logger = logger;
        _teamRepository = teamRepository;
    }

    public async Task Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.Id, cancellationToken);
        if (team == null)
        {
            _logger.LogWarning("Attempted to update non-existent Team (ID: {TeamId}).", request.Id);
            throw new KeyNotFoundException($"Team with ID {request.Id} not found.");
        }

        team.UpdateDetails(request.Name, request.LogoUrl);

        await _teamRepository.UpdateAsync(team, cancellationToken);
    }
}