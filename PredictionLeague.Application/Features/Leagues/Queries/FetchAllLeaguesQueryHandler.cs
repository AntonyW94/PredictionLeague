using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchAllLeaguesQueryHandler : IRequestHandler<FetchAllLeaguesQuery, IEnumerable<LeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchAllLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<LeagueDto>> Handle(FetchAllLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                COUNT(lm.[UserId]) AS MemberCount,
                l.[Price],
                ISNULL(l.[EntryCode], 'Public') AS EntryCode,
                l.[EntryDeadline]
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN 
                [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            GROUP BY
                l.[Id],
                l.[Name],
                s.[Name],
                l.[Price],  
                ISNULL(l.[EntryCode], 'Public'),
                l.[EntryDeadline],
                s.[StartDate]
            ORDER BY
                s.[StartDate] DESC,
                l.[Name] ASC;";

        return await _dbConnection.QueryAsync<LeagueDto>(sql, cancellationToken);
    }
}