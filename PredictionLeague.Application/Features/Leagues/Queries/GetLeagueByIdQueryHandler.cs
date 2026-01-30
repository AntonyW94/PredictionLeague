using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueByIdQueryHandler : IRequestHandler<GetLeagueByIdQuery, LeagueDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public GetLeagueByIdQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        await _membershipService.EnsureApprovedMemberAsync(request.Id, request.CurrentUserId, cancellationToken);

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
                l.[PointsForCorrectResult]
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
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
                l.[PointsForCorrectResult];";

        return await _dbConnection.QuerySingleOrDefaultAsync<LeagueDto>(
            sql,
            cancellationToken,
            new { request.Id }
        );
    }
}