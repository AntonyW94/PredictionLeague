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
        const string sql = @"
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
                END AS bit) AS HasUserPredicted
            FROM 
                [Rounds] r
            JOIN 
                [Seasons] s ON r.[SeasonId] = s.[Id]
            WHERE
                r.[Deadline] > GETUTCDATE()
                AND r.[Status] = @PublishedStatus
                AND r.[SeasonId] IN (
                    SELECT l.SeasonId
                    FROM [Leagues] l
                    JOIN [LeagueMembers] lm ON l.Id = lm.LeagueId
                    WHERE lm.UserId = @UserId AND lm.Status = 'Approved'
                )
            ORDER BY
                r.[Deadline] ASC;";

        var parameters = new
        {
            request.UserId,
            PublishedStatus = nameof(RoundStatus.Published)
        };

        return await _dbConnection.QueryAsync<UpcomingRoundDto>(sql, cancellationToken, parameters);
    }
}