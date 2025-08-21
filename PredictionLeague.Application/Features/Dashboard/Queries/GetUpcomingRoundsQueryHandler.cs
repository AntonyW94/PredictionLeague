using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Domain.Common.Enumerations; 

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetUpcomingRoundsQueryHandler : IRequestHandler<GetUpcomingRoundsQuery, IEnumerable<UpcomingRoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetUpcomingRoundsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<UpcomingRoundDto>> Handle(GetUpcomingRoundsQuery request, CancellationToken cancellationToken)
    {
        var sql = $@"
                     WITH RoundPredictionCounts AS (
                        SELECT
                            r.Id AS RoundId,
                            COUNT(DISTINCT up.UserId) AS PredictionsCount
                        FROM Rounds r
                        LEFT JOIN Matches m ON m.RoundId = r.Id
                        LEFT JOIN UserPredictions up ON up.MatchId = m.Id
                        GROUP BY r.Id
                    ),
                    ActiveMemberCount AS (
                        SELECT
                            l.SeasonId,
                            COUNT(DISTINCT lm.UserId) AS MemberCount
                        FROM LeagueMembers lm
                        JOIN Leagues l ON lm.LeagueId = l.Id
                        WHERE lm.Status = @ApprovedStatus
                        GROUP BY l.SeasonId
                    )
                    SELECT
                        r.[Id],
                        s.[Name] AS SeasonName,
                        r.[RoundNumber],
                        r.[Deadline],
                        CAST(CASE 
                            WHEN EXISTS (
                                SELECT 1 
                                FROM [UserPredictions] up
                                JOIN [Matches] m ON up.MatchId = m.Id
                                WHERE m.RoundId = r.Id AND up.UserId = @UserId
                            ) THEN 1 
                            ELSE 0 
                        END AS bit) AS HasUserPredicted,
                        CAST(CASE
                            WHEN rpc.PredictionsCount >= amc.MemberCount AND amc.MemberCount > 0 THEN 1
                            ELSE 0
                        END AS BIT) AS AllPredictionsIn
                    FROM 
                        [Rounds] r
                    JOIN 
                        [Seasons] s ON r.[SeasonId] = s.[Id]
                    LEFT JOIN 
                        RoundPredictionCounts rpc ON r.Id = rpc.RoundId
                    LEFT JOIN 
                        ActiveMemberCount amc ON r.SeasonId = amc.SeasonId
                    WHERE
                        r.[Status] = @PublishedStatus
                        {(!request.IsAdmin ? "AND r.[Deadline] > GETUTCDATE()" : string.Empty)}
                        AND r.[SeasonId] IN (
                            SELECT l.SeasonId
                            FROM [Leagues] l
                            JOIN [LeagueMembers] lm ON l.Id = lm.LeagueId
                            WHERE lm.UserId = @UserId AND lm.Status = @ApprovedStatus
                        )
                    ORDER BY
                        r.[Deadline] ASC";

        var parameters = new
        {
            request.UserId,
            PublishedStatus = nameof(RoundStatus.Published),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
        };

        return await _dbConnection.QueryAsync<UpcomingRoundDto>(sql, cancellationToken, parameters);
    }
}