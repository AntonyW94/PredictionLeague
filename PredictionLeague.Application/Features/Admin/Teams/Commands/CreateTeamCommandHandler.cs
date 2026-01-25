using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateTeamCommandHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService)
    {
        _teamRepository = teamRepository;
        _currentUserService = currentUserService;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        _currentUserService.EnsureAdministrator();

        var team = Team.Create(request.Name, request.ShortName, request.LogoUrl, request.Abbreviation, request.ApiTeamId);

        var createdTeam = await _teamRepository.CreateAsync(team, cancellationToken);
        return new TeamDto(createdTeam.Id, createdTeam.Name, request.ShortName, createdTeam.LogoUrl, request.Abbreviation, request.ApiTeamId);
    }
}