using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server read behind <see cref="ILeagueEmailRecipientQuery"/>. One statement where two notification handlers had the
/// same one each.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueEmailRecipientQuery(IApplicationReadDbConnection dbConnection) : ILeagueEmailRecipientQuery
{
    public async Task<LeagueEmailRecipientRow?> ExecuteAsync(string userId, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Email],
                u.[FirstName],
                s.[Name] AS [SeasonName]
            FROM
                [AspNetUsers] u
            CROSS JOIN
                [Seasons] s
            WHERE
                u.[Id] = @UserId
                AND s.[Id] = @SeasonId;";

        return await dbConnection.QuerySingleOrDefaultAsync<LeagueEmailRecipientRow>(
            sql, cancellationToken, new { UserId = userId, SeasonId = seasonId });
    }
}
