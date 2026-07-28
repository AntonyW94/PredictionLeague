using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Constants;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetCreateLeaguePageDataQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetCreateLeaguePageDataQuery, CreateLeaguePageData>
{
    public async Task<CreateLeaguePageData> Handle(GetCreateLeaguePageDataQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc],
                CAST(CASE WHEN c.[Type] = 1 THEN 1 ELSE 0 END AS bit) AS IsTournament
            FROM [Seasons] s
            JOIN [Competitions] c ON s.[CompetitionId] = c.[Id]
            WHERE s.[IsActive] = 1
            ORDER BY s.[StartDateUtc] DESC;";

        var seasons = await dbConnection.QueryAsync<SeasonLookupQueryResult>(sql, cancellationToken);

        return new CreateLeaguePageData
        {
            Seasons = seasons
                .Select(s => new SeasonLookupDto(
                    s.Id,
                    s.Name,
                    s.StartDateUtc,
                    s.IsTournament))
                .ToList(),
            DefaultPointsForExactScore = PublicLeagueSettings.PointsForExactScore,
            DefaultPointsForCorrectResult = PublicLeagueSettings.PointsForCorrectResult
        };
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record SeasonLookupQueryResult(
        int Id,
        string Name,
        DateTime StartDateUtc,
        bool IsTournament);
}