using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Team>
{
    private readonly ITeamRepository _teamRepository;

    public CreateTeamCommandHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<Team> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var newTeam = new Team
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl
        };

        return await _teamRepository.AddAsync(newTeam);
    }
}