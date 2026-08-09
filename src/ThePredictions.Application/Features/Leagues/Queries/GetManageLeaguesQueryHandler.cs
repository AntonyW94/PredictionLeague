using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetManageLeaguesQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetManageLeaguesQuery, ManageLeaguesDto>
{
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
                l.[EntryDeadlineUtc],
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                CASE
                    WHEN l.[EntryCode] IS NULL THEN 'Public'
                    WHEN l.[AdministratorUserId] = @UserId THEN 'MyPrivate'
                    ELSE 'OtherPrivate'
                END AS LeagueCategory
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            GROUP BY
                l.[Id], l.[Name], s.[Name], l.[Price], l.[EntryCode], l.[EntryDeadlineUtc], l.[PointsForExactScore], l.[PointsForCorrectResult], s.[StartDateUtc], l.[AdministratorUserId]
            ORDER BY
                s.[StartDateUtc] DESC, l.[Name] ASC;";

        var allLeagues = await dbConnection.QueryAsync<LeagueWithCategory>(
            sql,
            cancellationToken,
            new { request.UserId });

        var result = new ManageLeaguesDto();
        var leagues = allLeagues.ToList();

        if (request.IsAdmin)
        {
            result.PublicLeagues = leagues
                .Where(l => l.LeagueCategory == "Public")
                .Select(ToLeagueDto)
                .ToList();
        }

        result.MyPrivateLeagues = leagues
            .Where(l => l.LeagueCategory == "MyPrivate")
            .Select(ToLeagueDto)
            .ToList();

        if (request.IsAdmin)
        {
            result.OtherPrivateLeagues = leagues
                .Where(l => l.LeagueCategory == "OtherPrivate")
                .Select(ToLeagueDto)
                .ToList();
        }

        return result;
    }

    private static LeagueDto ToLeagueDto(LeagueWithCategory league) => new(
        league.Id,
        league.Name,
        league.SeasonName,
        league.MemberCount,
        league.Price,
        league.EntryCode,
        league.EntryDeadlineUtc,
        league.PointsForExactScore,
        league.PointsForCorrectResult);

    // internal so a test can supply rows for the visibility filter above; InternalsVisibleTo already
    // exposes this assembly to ThePredictions.Application.Tests.Unit.
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal record LeagueWithCategory(
        int Id,
        string Name,
        string SeasonName,
        int MemberCount,
        decimal Price,
        string EntryCode,
        DateTime EntryDeadlineUtc,
        int PointsForExactScore,
        int PointsForCorrectResult,
        string LeagueCategory);
}
