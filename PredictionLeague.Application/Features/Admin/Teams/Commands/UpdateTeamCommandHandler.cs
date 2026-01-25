using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards;

namespace PredictionLeague.Application.Features.Admin.Teams.Commands;

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentUserService _currentUserService;

    public UpdateTeamCommandHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService)
    {
        _teamRepository = teamRepository;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        _currentUserService.EnsureAdministrator();

        var team = await _teamRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, team, "Team");

        team.UpdateDetails(request.Name, request.ShortName, request.LogoUrl, request.Abbreviation, request.ApiTeamId);

        await _teamRepository.UpdateAsync(team, cancellationToken);
    }
}