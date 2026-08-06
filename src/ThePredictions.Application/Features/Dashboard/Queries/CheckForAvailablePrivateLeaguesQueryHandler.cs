using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class CheckForAvailablePrivateLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<CheckForAvailablePrivateLeaguesQuery, bool>
{
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

        return await dbConnection.QuerySingleOrDefaultAsync<bool>(sql, cancellationToken, new { request.UserId });
    }
}
