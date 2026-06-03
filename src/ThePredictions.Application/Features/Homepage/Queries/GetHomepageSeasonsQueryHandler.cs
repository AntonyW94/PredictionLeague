using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Homepage;

namespace ThePredictions.Application.Features.Homepage.Queries;

public class GetHomepageSeasonsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetHomepageSeasonsQuery, IEnumerable<HomepageSeasonDto>>
{
    public async Task<IEnumerable<HomepageSeasonDto>> Handle(
        GetHomepageSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                c.[Type] AS CompetitionType,
                s.[StartDateUtc],
                s.[EndDateUtc],
                CASE
                    WHEN GETUTCDATE() BETWEEN s.[StartDateUtc] AND s.[EndDateUtc] THEN 1
                    ELSE 0
                END AS IsInProgress,
                CASE
                    WHEN s.[StartDateUtc] > GETUTCDATE() THEN 1
                    ELSE 0
                END AS IsUpcoming,
                ISNULL(stats.[LeagueCount], 0) AS LeagueCount,
                ISNULL(players.[PlayerCount], 0) AS PlayerCount,
                ISNULL(stats.[TotalPrizeFund], 0) AS TotalPrizeFund
            FROM [Seasons] s
            JOIN [Competitions] c ON s.[CompetitionId] = c.[Id]
            LEFT JOIN (
                SELECT
                    lf.[SeasonId],
                    COUNT(DISTINCT lf.[Id]) AS LeagueCount,
                    SUM(lf.[Price] * lf.[MemberCount] + ISNULL(lf.[PrizeFundOverride], 0)) AS TotalPrizeFund
                FROM (
                    SELECT
                        l.[Id],
                        l.[SeasonId],
                        l.[Price],
                        l.[PrizeFundOverride],
                        (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = 'Approved') AS MemberCount
                    FROM [Leagues] l
                ) lf
                GROUP BY
                    lf.[SeasonId]
            ) stats ON s.[Id] = stats.[SeasonId]
            LEFT JOIN (
                SELECT
                    l.[SeasonId],
                    COUNT(DISTINCT lm.[UserId]) AS PlayerCount
                FROM [LeagueMembers] lm
                JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
                WHERE lm.[Status] = 'Approved'
                GROUP BY
                    l.[SeasonId]
            ) players ON s.[Id] = players.[SeasonId]
            WHERE
                s.[EndDateUtc] >= GETUTCDATE()
            ORDER BY
                s.[StartDateUtc]";

        return await dbConnection.QueryAsync<HomepageSeasonDto>(sql, cancellationToken);
    }
}
