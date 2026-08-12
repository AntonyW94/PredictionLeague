using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Prizes.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Prizes;

/// <summary>The SQL Server read behind <see cref="IPrizeSchemeSeasonQuery"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class PrizeSchemeSeasonQuery(IApplicationReadDbConnection dbConnection) : IPrizeSchemeSeasonQuery
{
    public async Task<PrizeSchemeSeasonRow?> ExecuteAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[NumberOfRounds],
                s.[StartDateUtc],
                s.[EndDateUtc]
            FROM
                [Seasons] s
            WHERE
                s.[Id] = @SeasonId;";

        return await dbConnection.QuerySingleOrDefaultAsync<PrizeSchemeSeasonRow>(
            sql, cancellationToken, new { SeasonId = seasonId });
    }
}
