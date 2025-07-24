using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetPublicLeaguesQueryHandler : IRequestHandler<GetPublicLeaguesQuery, IEnumerable<PublicLeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetPublicLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<PublicLeagueDto>> Handle(GetPublicLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS 'SeasonName',
                l.[Price],
                ISNULL(l.[EntryDeadline], '1900-01-01') AS 'EntryDeadline',
                lm.[Status] AS 'Status'
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN 
                [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId] AND lm.[UserId] = @UserId
            WHERE 
                l.[EntryCode] IS NULL
            ORDER BY 
                s.[StartDate] DESC, l.[Name];";

        return await _dbConnection.QueryAsync<PublicLeagueDto>(sql, cancellationToken, new { request.UserId });
    }
}