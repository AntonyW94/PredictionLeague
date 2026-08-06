using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetPendingRequestsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetPendingRequestsQuery, IEnumerable<LeagueRequestDto>>
{
    public async Task<IEnumerable<LeagueRequestDto>> Handle(GetPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                s.[Name] AS [SeasonName],
                lm.[Status],
                lm.[JoinedAtUtc],
                l.[EntryDeadlineUtc],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [AdminName],
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) AS [MemberCount],
                l.[Price] AS [EntryFee],
                (l.[Price] * (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) + ISNULL(l.[PrizeFundOverride], 0)) AS [PotValue]
            FROM
                [LeagueMembers] lm
            JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN
                [AspNetUsers] u ON l.[AdministratorUserId] = u.[Id]
            WHERE
                lm.[UserId] = @UserId
                AND (
                    lm.[Status] = @PendingStatus
                    OR 
                    (lm.[Status] = @RejectedStatus AND lm.[IsAlertDismissed] = 0)
                )
            ORDER BY
                lm.[JoinedAtUtc] DESC";

        var requests = await dbConnection.QueryAsync<LeagueRequestQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PendingStatus = nameof(LeagueMemberStatus.Pending),
                RejectedStatus = nameof(LeagueMemberStatus.Rejected)
            });

        return requests.Select(r => new LeagueRequestDto(
            r.LeagueId,
            r.LeagueName,
            r.SeasonName,
            r.Status,
            r.JoinedAtUtc,
            r.EntryDeadlineUtc,
            r.AdminName,
            r.MemberCount,
            r.EntryFee,
            r.PotValue));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueRequestQueryResult(
        int LeagueId,
        string LeagueName,
        string SeasonName,
        LeagueMemberStatus Status,
        DateTime JoinedAtUtc,
        DateTime EntryDeadlineUtc,
        string AdminName,
        int MemberCount,
        decimal EntryFee,
        decimal PotValue);
}
