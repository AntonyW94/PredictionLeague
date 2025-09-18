using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;

    public CreateTeamCommandHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = Team.Create(request.Name, request.ShortName, request.LogoUrl, request.Abbreviation, request.ApiTeamId);

        var createdTeam = await _teamRepository.CreateAsync(team, cancellationToken);
        return new TeamDto(createdTeam.Id, createdTeam.Name, request.ShortName, createdTeam.LogoUrl, request.Abbreviation, request.ApiTeamId);
    }
}