using MediatR;
using ThePredictions.Contracts.Admin.Teams;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>
/// The administrator's team list, either every team or only those playing in one season.
/// </summary>
public class FetchAllTeamsQueryHandler(ITeamsQuery teamsQuery, ISeasonTeamsQuery seasonTeamsQuery)
    : IRequestHandler<FetchAllTeamsQuery, IEnumerable<TeamDto>>
{
    public async Task<IEnumerable<TeamDto>> Handle(FetchAllTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = request.SeasonId is { } seasonId
            ? await seasonTeamsQuery.ExecuteAsync(seasonId, cancellationToken)
            : await teamsQuery.ExecuteAsync(cancellationToken);

        return TeamMapping.InNameOrder(teams).Select(TeamMapping.ToDto).ToList();
    }
}
