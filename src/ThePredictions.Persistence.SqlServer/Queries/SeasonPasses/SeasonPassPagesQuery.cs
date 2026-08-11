using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.SeasonPasses.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.SeasonPasses;

/// <summary>
/// The SQL Server reads behind <see cref="ISeasonPassPagesQuery"/>.
///
/// Four reads where there were four statements, one per screen, repeating the same building blocks between them. What is
/// gone: three calls to <c>GETUTCDATE()</c>, the <c>NOT EXISTS</c> that checked whether a pass was already held, the
/// <c>EXISTS</c> and <c>NOT EXISTS</c> pair that made the available and past pages complements of each other, the
/// trial-eligibility count, two <c>CASE WHEN ... IS NOT NULL</c> price tests, a tier comparison, a <c>MIN</c> over future
/// deadlines and two <c>ORDER BY</c> clauses.
/// </summary>
/// <remarks>
/// The holder counts stay here. Each is a count of rows in a scoped set, and the interesting part - that participation is
/// counted from passes rather than from league membership - is a decision about which table to read, which is the read's
/// own business.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonPassPagesQuery(IApplicationReadDbConnection dbConnection) : ISeasonPassPagesQuery
{
    public async Task<SeasonPassPagesData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);

        if (seasons.Count == 0)
            return new SeasonPassPagesData(seasons, [], [], []);

        var leagues = await GetLeaguesAsync(cancellationToken);
        var holderCounts = await GetHolderCountsAsync(cancellationToken);
        var heldPasses = await GetHeldPassesAsync(userId, cancellationToken);

        return new SeasonPassPagesData(seasons, leagues, holderCounts, heldPasses);
    }

    /// <summary>
    /// Every season, active or not. The options page is reached by id and has to answer for a season that has been
    /// retired; whether an inactive season may be offered is a rule.
    /// </summary>
    private async Task<IReadOnlyList<SeasonPassSeasonRow>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc],
                s.[IsActive],
                c.[LogoUrl] AS [CompetitionLogoUrl],
                c.[Description] AS [CompetitionDescription],
                s.[PassStandardPrice] AS [StandardPrice],
                s.[PassPremiumPrice] AS [PremiumPrice]
            FROM
                [Seasons] s
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId];";

        return (await dbConnection.QueryAsync<SeasonPassSeasonRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<SeasonLeagueEntryRow>> GetLeaguesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[SeasonId],
                l.[Id] AS [LeagueId],
                l.[EntryDeadlineUtc]
            FROM
                [Leagues] l;";

        return (await dbConnection.QueryAsync<SeasonLeagueEntryRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<SeasonPassHolderCountRow>> GetHolderCountsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[SeasonId],
                COUNT(*) AS [HolderCount]
            FROM
                [SeasonPasses] sp
            GROUP BY
                sp.[SeasonId];";

        return (await dbConnection.QueryAsync<SeasonPassHolderCountRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<HeldSeasonPassRow>> GetHeldPassesAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[SeasonId],
                sp.[Tier],
                sp.[Source],
                sp.[AmountPaid],
                sp.[CreatedAtUtc]
            FROM
                [SeasonPasses] sp
            WHERE
                sp.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<HeldSeasonPassRow>(sql, cancellationToken, new { UserId = userId })).ToList();
    }
}
