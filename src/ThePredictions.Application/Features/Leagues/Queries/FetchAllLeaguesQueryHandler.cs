using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class FetchAllLeaguesQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<FetchAllLeaguesQuery, IEnumerable<LeagueDto>>
{
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
                l.[EntryDeadlineUtc],
                l.[PointsForExactScore],
                l.[PointsForCorrectResult]
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            GROUP BY
                l.[Id],
                l.[Name],
                s.[Name],
                l.[Price],
                ISNULL(l.[EntryCode], 'Public'),
                l.[EntryDeadlineUtc],
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                s.[StartDateUtc]
            ORDER BY
                s.[StartDateUtc] DESC,
                l.[Name] ASC;";

        var leagues = await dbConnection.QueryAsync<LeagueQueryResult>(sql, cancellationToken);

        // The SELECT returns the first nine LeagueDto values only; the remaining five keep their
        // constructor defaults. Materialising straight into LeagueDto could never work here - Dapper
        // needs a constructor whose parameter count matches the column count, and LeagueDto has 14.
        return leagues.Select(l => new LeagueDto(
            l.Id,
            l.Name,
            l.SeasonName,
            l.MemberCount,
            l.Price,
            l.EntryCode,
            l.EntryDeadlineUtc,
            l.PointsForExactScore,
            l.PointsForCorrectResult));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueQueryResult(
        int Id,
        string Name,
        string SeasonName,
        int MemberCount,
        decimal Price,
        string EntryCode,
        DateTime EntryDeadlineUtc,
        int PointsForExactScore,
        int PointsForCorrectResult);
}