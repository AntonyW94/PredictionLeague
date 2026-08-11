using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ILeagueSeasonRoundsQuery"/>.
///
/// One statement replacing two, each of which had grouped, counted, filtered and ordered on its way out. Nothing here
/// does any of that - and the stage text comes back raw rather than classified, so the collation-dependent
/// <c>LIKE '%Group%'</c> cannot come back with it.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueSeasonRoundsQuery(IApplicationReadDbConnection dbConnection) : ILeagueSeasonRoundsQuery
{
    public async Task<IReadOnlyList<LeagueSeasonRoundRow>> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        // Drafts included: whether a period with nothing but drafts in it is worth offering is a rule, and both
        // pickers apply it in C#.
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                r.[RoundNumber],
                r.[StartDateUtc],
                r.[Status],
                trm.[Stages]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            LEFT JOIN
                [TournamentRoundMappings] trm ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
            WHERE
                l.[Id] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueSeasonRoundRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }
}
