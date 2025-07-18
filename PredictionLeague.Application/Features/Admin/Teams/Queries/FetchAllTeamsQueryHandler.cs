using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class FetchAllTeamsQueryHandler : IRequestHandler<FetchAllTeamsQuery, IEnumerable<TeamDto>>
{
    private readonly ITeamRepository _teamRepository;

    public FetchAllTeamsQueryHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<IEnumerable<TeamDto>> Handle(FetchAllTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await _teamRepository.GetAllAsync();
        return teams.Select(team => new TeamDto(team.Id, team.Name, team.LogoUrl));
    }
}