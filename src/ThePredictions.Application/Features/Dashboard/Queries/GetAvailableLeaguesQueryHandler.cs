using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetAvailableLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetAvailableLeaguesQuery, IEnumerable<AvailableLeagueDto>>
{
    public async Task<IEnumerable<AvailableLeagueDto>> Handle(GetAvailableLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                l.[Price],
                l.[EntryDeadlineUtc],
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) AS MemberCount,
                (l.[Price] * (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) + ISNULL(l.[PrizeFundOverride], 0)) AS EstPot,
                CAST(CASE WHEN l.[EntryCode] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsPrivate
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE
                (l.[EntryCode] IS NULL OR l.[IsListed] = 1)            -- public leagues, plus private leagues the admin has chosen to list
                AND l.[EntryDeadlineUtc] > GETUTCDATE()
                AND NOT EXISTS (
                    SELECT 1
                    FROM [LeagueMembers] lm
                    WHERE lm.[LeagueId] = l.[Id] AND lm.[UserId] = @UserId
                )
                AND EXISTS (                                            -- acquire-first: only show leagues for seasons the user holds a pass for
                    SELECT 1
                    FROM [SeasonPasses] sp
                    WHERE sp.[UserId] = @UserId AND sp.[SeasonId] = l.[SeasonId]
                )
            ORDER BY
                s.[StartDateUtc] DESC, 
                l.[Name];";

        var leagues = await dbConnection.QueryAsync<AvailableLeagueQueryResult>(sql, cancellationToken, new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return leagues.Select(l => new AvailableLeagueDto(
            l.Id,
            l.Name,
            l.SeasonName,
            l.Price,
            l.EntryDeadlineUtc,
            l.MemberCount,
            l.EstPot,
            l.IsPrivate));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record AvailableLeagueQueryResult(
        int Id,
        string Name,
        string SeasonName,
        decimal Price,
        DateTime EntryDeadlineUtc,
        int MemberCount,
        decimal EstPot,
        bool IsPrivate);
}