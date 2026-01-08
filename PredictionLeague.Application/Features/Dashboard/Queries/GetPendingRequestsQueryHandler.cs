using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetPendingRequestsQueryHandler : IRequestHandler<GetPendingRequestsQuery, IEnumerable<LeagueRequestDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetPendingRequestsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<LeagueRequestDto>> Handle(GetPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                s.[Name] AS [SeasonName],
                lm.[Status],
                lm.[JoinedAt] AS [SentAt],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [AdminName],
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) AS [MemberCount],
                l.[Price] AS [EntryFee],
                COALESCE(l.[PrizeFundOverride], l.[Price] * (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus)) AS [PotValue]
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
                lm.[JoinedAt] DESC";

        return await _dbConnection.QueryAsync<LeagueRequestDto>(
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