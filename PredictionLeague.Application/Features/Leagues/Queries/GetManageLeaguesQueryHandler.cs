using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetManageLeaguesQueryHandler : IRequestHandler<GetManageLeaguesQuery, ManageLeaguesDto>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetManageLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<ManageLeaguesDto> Handle(GetManageLeaguesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                COUNT(lm.[UserId]) AS MemberCount,
                l.[Price],
                ISNULL(l.[EntryCode], 'Public') AS EntryCode,
                l.[EntryDeadline],
                CASE
                    WHEN l.[EntryCode] IS NULL THEN 'Public'
                    WHEN l.[AdministratorUserId] = @UserId THEN 'MyPrivate'
                    ELSE 'OtherPrivate'
                END AS LeagueCategory
            FROM 
                [dbo].[Leagues] l
            JOIN 
                [dbo].[Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN 
                [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            GROUP BY
                l.[Id], l.[Name], s.[Name], l.[Price], l.[EntryCode], l.[EntryDeadline], s.[StartDate], l.[AdministratorUserId]
            ORDER BY
                s.[StartDate] DESC, l.[Name] ASC;";

        var allLeagues = await _dbConnection.QueryAsync<LeagueWithCategory>(
            sql,
            cancellationToken,
            new { request.UserId });

        var result = new ManageLeaguesDto();
        var leagues = allLeagues.ToList();

        if (request.IsAdmin)
        {
            result.PublicLeagues = leagues
                .Where(l => l.LeagueCategory == "Public")
                .Select(l => l.ToLeagueDto())
                .ToList();
        }

        result.MyPrivateLeagues = leagues
            .Where(l => l.LeagueCategory == "MyPrivate")
            .Select(l => l.ToLeagueDto())
            .ToList();

        if (request.IsAdmin)
        {
            result.OtherPrivateLeagues = leagues
                .Where(l => l.LeagueCategory == "OtherPrivate")
                .Select(l => l.ToLeagueDto())
                .ToList();
        }

        return result;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueWithCategory(
        int Id,
        string Name,
        string SeasonName,
        int MemberCount,
        decimal Price,
        string EntryCode,
        DateTime EntryDeadline,
        string LeagueCategory)
    {
        public LeagueDto ToLeagueDto() => new(Id, Name, SeasonName, MemberCount, Price, EntryCode, EntryDeadline);
    }
}