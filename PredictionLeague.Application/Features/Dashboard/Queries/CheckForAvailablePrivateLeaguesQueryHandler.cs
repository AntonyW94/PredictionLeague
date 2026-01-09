using MediatR;
using PredictionLeague.Application.Data;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class CheckForAvailablePrivateLeaguesQueryHandler : IRequestHandler<CheckForAvailablePrivateLeaguesQuery, bool>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public CheckForAvailablePrivateLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<bool> Handle(CheckForAvailablePrivateLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 
                FROM [Leagues] l
                WHERE l.[EntryCode] IS NOT NULL 
                AND l.[EntryDeadlineUtc] > GETUTCDATE()                    
                AND NOT EXISTS (                                        
                    SELECT 1 
                    FROM [LeagueMembers] lm 
                    WHERE lm.[LeagueId] = l.[Id] AND lm.[UserId] = @UserId
                )
            )
            THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT) END";

        return await _dbConnection.QuerySingleOrDefaultAsync<bool>(sql, cancellationToken, new { request.UserId });
    }
}