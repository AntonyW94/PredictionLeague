using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueByIdQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueByIdQuery, LeagueDto?>
{
    public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.Id, request.CurrentUserId, cancellationToken);

        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                COUNT(lm.[UserId]) AS MemberCount,
                l.[Price],
                ISNULL(l.[EntryCode], 'Public') AS EntryCode,
                ISNULL(l.[EntryDeadlineUtc], '1900-01-01') AS 'EntryDeadlineUtc',
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                l.[SeasonId],
                CAST(CASE WHEN c.[Type] = 1 THEN 1 ELSE 0 END AS bit) AS IsTournament,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM [LeaguePrizeScheme] lps WHERE lps.[LeagueId] = l.[Id]) THEN 1 ELSE 0 END AS bit) AS HasPrizeScheme,
                l.[RequiresMemberApproval],
                l.[IsListed]
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            LEFT JOIN
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE
                l.[Id] = @Id
            GROUP BY
                l.[Id],
                l.[Name],
                s.[Name],
                l.[Price],
                ISNULL(l.[EntryCode], 'Public'),
                ISNULL(l.[EntryDeadlineUtc], '1900-01-01'),
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                l.[SeasonId],
                c.[Type],
                l.[RequiresMemberApproval],
                l.[IsListed];";

        return await dbConnection.QuerySingleOrDefaultAsync<LeagueDto>(
            sql,
            cancellationToken,
            new { request.Id }
        );
    }
}