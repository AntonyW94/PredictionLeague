using MediatR;
using ThePredictions.Contracts.Admin.Teams;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>One team, for the administrator's edit screen.</summary>
public class GetTeamByIdQueryHandler(ITeamsQuery teamsQuery)
    : IRequestHandler<GetTeamByIdQuery, TeamDto>
{
    public async Task<TeamDto> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var teams = await teamsQuery.ExecuteAsync(cancellationToken);

        var team = teams.SingleOrDefault(candidate => candidate.Id == request.Id)
                   ?? throw new EntityNotFoundException("Team", request.Id);

        return TeamMapping.ToDto(team);
    }
}
