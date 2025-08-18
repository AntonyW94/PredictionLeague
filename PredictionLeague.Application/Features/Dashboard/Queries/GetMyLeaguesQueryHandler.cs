using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMyLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH LeagueUserPoints AS (
                SELECT
                    lm.UserId,
                    l.Id AS LeagueId,
                    SUM(ISNULL(up.PointsAwarded, 0)) AS TotalPoints
                FROM Leagues l
                JOIN Seasons s ON l.SeasonId = s.Id
                JOIN Rounds r ON s.Id = r.SeasonId
                JOIN Matches m ON r.Id = m.RoundId
                JOIN LeagueMembers lm ON l.Id = lm.LeagueId
                LEFT JOIN UserPredictions up ON m.Id = up.MatchId AND lm.UserId = up.UserId
                WHERE lm.Status = @ApprovedStatus
                GROUP BY lm.UserId, l.Id
            ),
            
            RankedUsers AS (
                SELECT
                    UserId,
                    LeagueId,
                    RANK() OVER (PARTITION BY LeagueId ORDER BY TotalPoints DESC) AS OverallRank
                FROM LeagueUserPoints
            )

            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                lm.[Status],
                ru.OverallRank AS Rank,
                (SELECT COUNT(*) FROM LeagueMembers WHERE LeagueId = l.Id AND Status = @ApprovedStatus) AS MemberCount
            FROM 
                [Leagues] l
            JOIN 
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN 
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            LEFT JOIN
                RankedUsers ru ON ru.UserId = lm.UserId AND ru.LeagueId = l.Id
            WHERE 
                lm.[UserId] = @UserId
            ORDER BY 
                s.[StartDate] DESC, [MemberCount] DESC, l.[Name];";

        return await _dbConnection.QueryAsync<MyLeagueDto>(sql, cancellationToken, new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}