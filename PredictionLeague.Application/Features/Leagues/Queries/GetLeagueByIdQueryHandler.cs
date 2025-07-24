using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueByIdQueryHandler : IRequestHandler<GetLeagueByIdQuery, LeagueDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetLeagueByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<LeagueDto?> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                COUNT(lm.[UserId]) AS MemberCount,
                l.[Price],
                ISNULL(l.[EntryCode], 'Public') AS EntryCode,
                ISNULL(l.[EntryDeadline], '1900-01-01') AS 'EntryDeadline'
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN 
                [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE 
                l.[Id] = @Id
            GROUP BY
                l.[Id],
                l.[Name],
                s.[Name],
                l.[Price],
                ISNULL(l.[EntryCode], 'Public'),
                ISNULL(l.[EntryDeadline], '1900-01-01');";

        return await _dbConnection.QuerySingleOrDefaultAsync<LeagueDto>(
            sql,
            cancellationToken,
            new { request.Id }
        );
    }
}