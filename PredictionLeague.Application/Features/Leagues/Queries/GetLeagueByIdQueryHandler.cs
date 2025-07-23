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
                ISNULL(l.[EntryCode], 'Public') AS EntryCode,
                l.[EntryDeadline]
            FROM [dbo].[Leagues] l
            JOIN [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE l.[Id] = @Id
            GROUP BY
                l.[Id],
                l.[Name],
                s.[Name],
                l.[EntryCode],
                l.[EntryDeadline];";

        return await _dbConnection.QuerySingleOrDefaultAsync<LeagueDto>(
            sql,
            cancellationToken,
            new { request.Id }
        );
    }
}