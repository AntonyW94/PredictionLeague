using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;

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
        const string sql = @"
            SELECT TOP 1 WITH TIES
                r.[Id],
                s.[Name] AS SeasonName,
                r.[RoundNumber],
                r.[Deadline]
            FROM [dbo].[Rounds] r
            JOIN [dbo].[Seasons] s ON r.[SeasonId] = s.[Id]
            WHERE
                r.[Deadline] > GETUTCDATE()
                AND r.[SeasonId] IN (
                    SELECT l.SeasonId
                    FROM [dbo].[Leagues] l
                    JOIN [dbo].[LeagueMembers] lm ON l.Id = lm.LeagueId
                    WHERE lm.UserId = @UserId
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[UserPredictions] up
                    JOIN [dbo].[Matches] m ON up.MatchId = m.Id
                    WHERE m.RoundId = r.Id AND up.UserId = @UserId
                )
            ORDER BY
                ROW_NUMBER() OVER(PARTITION BY r.SeasonId ORDER BY r.Deadline ASC);";

        return await _dbConnection.QueryAsync<UpcomingRoundDto>(sql, cancellationToken, new { request.UserId });
    }
}