using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ILeagueRoundsQuery"/>.
///
/// One statement replacing two that were identical apart from a status filter and an <c>ORDER BY</c> - both of which
/// are rules, and both of which differ between the two callers.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueRoundsQuery(IApplicationReadDbConnection dbConnection) : ILeagueRoundsQuery
{
    public async Task<IReadOnlyList<LeagueRoundRow>> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                ) AS [MatchCount]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            WHERE
                l.[Id] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueRoundRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }
}
