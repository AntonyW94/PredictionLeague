using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IEarlierRoundStatusesQuery"/>. Scoping only: which rounds come
/// earlier in this season. Whether that means the early reminder milestones are allowed is the caller's rule.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class EarlierRoundStatusesQuery(IApplicationReadDbConnection dbConnection) : IEarlierRoundStatusesQuery
{
    public async Task<IReadOnlyList<RoundStatus>> ExecuteAsync(
        int seasonId, int roundNumber, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Status]
            FROM
                [Rounds] r
            WHERE
                r.[SeasonId] = @SeasonId
                AND r.[RoundNumber] < @RoundNumber;";

        var statuses = await dbConnection.QueryAsync<string>(
            sql, cancellationToken, new { SeasonId = seasonId, RoundNumber = roundNumber });

        return statuses.Select(Enum.Parse<RoundStatus>).ToList();
    }
}
