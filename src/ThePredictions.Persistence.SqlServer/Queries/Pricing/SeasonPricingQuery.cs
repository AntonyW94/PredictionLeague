using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Pricing;

/// <summary>
/// The SQL Server reads behind <see cref="ISeasonPricingQuery"/>.
///
/// What is gone: the overlap window that decided which seasons share the annual costs, the exclusion of free seasons, the
/// <c>TOP 1 ... ORDER BY [EndDateUtc] DESC</c> that picked the most recently finished comparable season, and a clock read
/// inline in the parameters rather than injected - which is why none of it could be tested.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonPricingQuery(IApplicationReadDbConnection dbConnection) : ISeasonPricingQuery
{
    public async Task<IReadOnlyList<SeasonPricingRow>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[CompetitionId],
                s.[NumberOfRounds],
                s.[StartDateUtc],
                s.[EndDateUtc],
                s.[PassStandardPrice] AS [StandardPrice]
            FROM
                [Seasons] s;";

        return (await dbConnection.QueryAsync<SeasonPricingRow>(sql, cancellationToken)).ToList();
    }

    /// <remarks>
    /// Distinct players, because somebody in two leagues of the same season is one participant. That is a count over a
    /// scoped set rather than a classification, so it stays here.
    /// </remarks>
    public async Task<int> CountApprovedParticipantsAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COUNT(DISTINCT lm.[UserId])
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            WHERE
                l.[SeasonId] = @SeasonId
                AND lm.[Status] = @ApprovedStatus;";

        return await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql, cancellationToken,
            new { SeasonId = seasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }
}
