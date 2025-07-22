using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDto?>
{
    private readonly ITeamRepository _teamRepository;

    public GetTeamByIdQueryHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<TeamDto?> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.Id, cancellationToken);
        return team == null ? null : new TeamDto(team.Id, team.Name, team.LogoUrl);
    }
}