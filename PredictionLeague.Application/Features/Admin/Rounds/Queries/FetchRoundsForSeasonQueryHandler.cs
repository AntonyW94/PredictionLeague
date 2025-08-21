using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class FetchRoundsForSeasonQueryHandler : IRequestHandler<FetchRoundsForSeasonQuery, IEnumerable<RoundWithAllPredictionsInDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchRoundsForSeasonQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<RoundWithAllPredictionsInDto>> Handle(FetchRoundsForSeasonQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH RoundPredictionCounts AS (
                SELECT
                    r.Id AS RoundId,
                    COUNT(DISTINCT up.UserId) AS PredictionsCount
                FROM Rounds r
                LEFT JOIN Matches m ON m.RoundId = r.Id
                LEFT JOIN UserPredictions up ON up.MatchId = m.Id
                WHERE r.SeasonId = @SeasonId
                GROUP BY r.Id
            ),
            ActiveMemberCount AS (
                SELECT
                    COUNT(DISTINCT lm.UserId) AS MemberCount
                FROM LeagueMembers lm
                JOIN Leagues l ON lm.LeagueId = l.Id
                WHERE l.SeasonId = @SeasonId AND lm.Status = 'Approved'
            )
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) as MatchCount,
                CAST(   
                        CASE
                            WHEN rpc.[PredictionsCount] >= amc.[MemberCount] AND amc.[MemberCount] > 0 THEN 1
                            ELSE 0
                        END AS BIT
                    ) AS AllPredictionsIn
            FROM
                [Rounds] r
            LEFT JOIN 
                [RoundPredictionCounts] rpc ON r.[Id] = rpc.[RoundId]
            CROSS JOIN 
                [ActiveMemberCount] amc
            WHERE
                r.[SeasonId] = @SeasonId
            ORDER BY
                r.[RoundNumber];";

        var queryResult = await _dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, new { request.SeasonId });

        return queryResult.Select(r => new RoundWithAllPredictionsInDto(
            r.Id,
            r.SeasonId,
            r.RoundNumber,
            r.StartDate,
            r.Deadline,
            Enum.Parse<RoundStatus>(r.Status),
            r.MatchCount,
            r.AllPredictionsIn
        )).ToList();
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int Id,
        int SeasonId,
        int RoundNumber,
        DateTime StartDate,
        DateTime Deadline,
        string Status,
        int MatchCount,
        bool AllPredictionsIn
    );
}