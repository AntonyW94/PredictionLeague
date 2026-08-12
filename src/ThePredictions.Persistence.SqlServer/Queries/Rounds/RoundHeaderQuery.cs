using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IRoundHeaderQuery"/>. One statement where the prediction page and the share card
/// each joined the same three tables themselves.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class RoundHeaderQuery(IApplicationReadDbConnection dbConnection) : IRoundHeaderQuery
{
    public async Task<RoundHeaderRow?> ExecuteAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                r.[RoundNumber],
                r.[DisplayName],
                r.[DeadlineUtc],
                s.[Id] AS [SeasonId],
                s.[Name] AS [SeasonName],
                s.[NumberOfRounds],
                c.[Type] AS [CompetitionType]
            FROM
                [Rounds] r
            INNER JOIN
                [Seasons] s ON s.[Id] = r.[SeasonId]
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                r.[Id] = @RoundId;";

        return await dbConnection.QuerySingleOrDefaultAsync<RoundHeaderRow>(sql, cancellationToken, new { RoundId = roundId });
    }
}
