using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetAvailableLeaguesQueryHandler : IRequestHandler<GetAvailableLeaguesQuery, IEnumerable<AvailableLeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetAvailableLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<AvailableLeagueDto>> Handle(GetAvailableLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                l.[Price],
                l.[EntryDeadline]
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE 
                l.[EntryCode] IS NULL                                   
                AND l.[EntryDeadline] > GETUTCDATE()                    
                AND NOT EXISTS (                                        
                    SELECT 1 
                    FROM [dbo].[LeagueMembers] lm 
                    WHERE lm.[LeagueId] = l.[Id] AND lm.[UserId] = @UserId
                )
            ORDER BY 
                s.[StartDate] DESC, l.[Name];";

        return await _dbConnection.QueryAsync<AvailableLeagueDto>(sql, cancellationToken, new { request.UserId });
    }
}