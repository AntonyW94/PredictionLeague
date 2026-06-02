using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

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

        return await dbConnection.QueryAsync<LeagueRequestDto>(
            sql,
            cancellationToken,
            new
            {
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PendingStatus = nameof(LeagueMemberStatus.Pending),
                RejectedStatus = nameof(LeagueMemberStatus.Rejected)
            });
    }
}