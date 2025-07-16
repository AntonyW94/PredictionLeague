using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class GetAllTeamsQueryHandler : IRequestHandler<FetchAllTeamsQuery, IEnumerable<TeamDto>>
{
    private readonly ITeamRepository _teamRepository;

    public GetAllTeamsQueryHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<IEnumerable<TeamDto>> Handle(FetchAllTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await _teamRepository.GetAllAsync();

        return teams.Select(t => new TeamDto
        {
            Id = t.Id,
            Name = t.Name,
            LogoUrl = t.LogoUrl
        });
    }
}