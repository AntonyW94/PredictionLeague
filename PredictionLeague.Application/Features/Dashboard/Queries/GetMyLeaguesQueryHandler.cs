using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;

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
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                lm.[Status]
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN 
                [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE 
                lm.[UserId] = @UserId
            ORDER BY 
                s.[StartDate] DESC, l.[Name];";

        return await _dbConnection.QueryAsync<MyLeagueDto>(sql, cancellationToken, new { request.UserId });
    }
}