using MediatR;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>
/// The teams playing in a season, for the badges shown on the season-pass page.
/// </summary>
/// <remarks>
/// Reads through <see cref="ISeasonTeamsQuery"/>, which the administrator's team list also uses. The two had a statement
/// each asking the same question in different shapes.
/// </remarks>
public class GetSeasonTeamsQueryHandler(ISeasonTeamsQuery seasonTeamsQuery)
    : IRequestHandler<GetSeasonTeamsQuery, IEnumerable<SeasonTeamDto>>
{
    public async Task<IEnumerable<SeasonTeamDto>> Handle(GetSeasonTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await seasonTeamsQuery.ExecuteAsync(request.SeasonId, cancellationToken);

        return TeamMapping.InNameOrder(teams)
            .Select(team => new SeasonTeamDto(team.Name, team.LogoUrl))
            .ToList();
    }
}
