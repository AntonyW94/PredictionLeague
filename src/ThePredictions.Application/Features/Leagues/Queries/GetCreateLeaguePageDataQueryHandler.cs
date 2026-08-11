using MediatR;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// What the create-a-league page needs: the seasons a league may be created in, and the scoring it starts with.
/// </summary>
public class GetCreateLeaguePageDataQueryHandler(ISeasonLookupQuery seasonLookupQuery)
    : IRequestHandler<GetCreateLeaguePageDataQuery, CreateLeaguePageData>
{
    public async Task<CreateLeaguePageData> Handle(
        GetCreateLeaguePageDataQuery request,
        CancellationToken cancellationToken)
    {
        var seasons = await seasonLookupQuery.ExecuteAsync(cancellationToken);

        return new CreateLeaguePageData
        {
            Seasons = seasons
                .Where(season => season.IsActive)
                .OrderByDescending(season => season.StartDateUtc)
                .Select(season => new SeasonLookupDto(
                    season.Id,
                    season.Name,
                    season.StartDateUtc,
                    season.CompetitionType == CompetitionType.Tournament))
                .ToList(),
            DefaultPointsForExactScore = PublicLeagueSettings.PointsForExactScore,
            DefaultPointsForCorrectResult = PublicLeagueSettings.PointsForCorrectResult
        };
    }
}
