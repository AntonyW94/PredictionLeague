using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Seasons;

/// <summary>
/// The SQL Server reads behind <see cref="ISeasonPassHoldersQuery"/>.
///
/// The one place in this refactor where the filtering, sorting and paging stay in the database, because they are choosing
/// which rows to return and a page cannot be taken without sorting first.
/// </summary>
/// <remarks>
/// The escaping of the name filter moved <b>in</b> here rather than out, which is the opposite direction to everything else.
/// It replaces <c>%</c>, <c>_</c> and <c>[</c> because those are <c>LIKE</c> wildcards - a fact about how this adapter
/// searches, not about what an administrator meant. Another adapter would escape differently or not at all.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonPassHoldersQuery(IApplicationReadDbConnection dbConnection) : ISeasonPassHoldersQuery
{
    /// <summary>
    /// The column filters, shared by both reads so the two can never disagree about what "matching" means. Every filter is
    /// skipped when its parameter is null, which keeps the statement a compile-time constant - the schema check cannot
    /// resolve SQL built at run time.
    /// </summary>
    private const string ColumnFilters = @"
                AND (@NameFilter IS NULL OR (u.[FirstName] + ' ' + u.[LastName]) LIKE '%' + @NameFilter + '%')
                AND (@AcquiredFromUtc IS NULL OR sp.[CreatedAtUtc] >= @AcquiredFromUtc)
                AND (@AcquiredBeforeUtc IS NULL OR sp.[CreatedAtUtc] < @AcquiredBeforeUtc)
                AND (@MinimumPaid IS NULL OR (sp.[AmountPaid] + sp.[SmsFeePaid]) >= @MinimumPaid)
                AND (@MaximumPaid IS NULL OR (sp.[AmountPaid] + sp.[SmsFeePaid]) <= @MaximumPaid)";

    private const string SummarySql = @"
        SELECT
            s.[Name] AS [SeasonName],
            m.[MatchingCount],
            m.[TotalCollected]
        FROM
            [Seasons] s
        CROSS APPLY
            (
                SELECT
                    COUNT(*) AS MatchingCount,
                    ISNULL(SUM(sp.[AmountPaid] + sp.[SmsFeePaid]), 0) AS TotalCollected
                FROM
                    [SeasonPasses] sp
                INNER JOIN
                    [AspNetUsers] u ON u.[Id] = sp.[UserId]
                WHERE
                    sp.[SeasonId] = s.[Id]" + ColumnFilters + @"
            ) m
        WHERE
            s.[Id] = @SeasonId;";

    /// <summary>
    /// The sort column is chosen by a parameter rather than by concatenating one in, so there is no way for a caller to
    /// reach the <c>ORDER BY</c> and the statement stays constant. Every branch but the chosen one evaluates to null for
    /// every row, contributing nothing to the ordering, and the row id breaks any remaining tie so a page boundary cannot
    /// shuffle.
    /// <para>
    /// <c>OFFSET</c> and <c>FETCH NEXT</c> cast their parameters because the schema check has to infer parameter types from
    /// the call site, and a row count it infers as anything but an integer fails to compile - which would skip this read
    /// instead of verifying it.
    /// </para>
    /// </summary>
    private const string PageSql = @"
        SELECT
            sp.[UserId],
            u.[FirstName] + ' ' + u.[LastName] AS [FullName],
            u.[Email],
            sp.[Tier],
            sp.[Source],
            sp.[AmountPaid],
            sp.[SmsFeePaid],
            sp.[CreatedAtUtc]
        FROM
            [SeasonPasses] sp
        INNER JOIN
            [AspNetUsers] u ON u.[Id] = sp.[UserId]
        WHERE
            sp.[SeasonId] = @SeasonId" + ColumnFilters + @"
        ORDER BY
            CASE WHEN @SortField = 'Name' AND @SortDescending = 0 THEN u.[FirstName] + ' ' + u.[LastName] END ASC,
            CASE WHEN @SortField = 'Name' AND @SortDescending = 1 THEN u.[FirstName] + ' ' + u.[LastName] END DESC,
            CASE WHEN @SortField = 'AcquiredAt' AND @SortDescending = 0 THEN sp.[CreatedAtUtc] END ASC,
            CASE WHEN @SortField = 'AcquiredAt' AND @SortDescending = 1 THEN sp.[CreatedAtUtc] END DESC,
            CASE WHEN @SortField = 'TotalPaid' AND @SortDescending = 0 THEN sp.[AmountPaid] + sp.[SmsFeePaid] END ASC,
            CASE WHEN @SortField = 'TotalPaid' AND @SortDescending = 1 THEN sp.[AmountPaid] + sp.[SmsFeePaid] END DESC,
            sp.[Id] ASC
        OFFSET CAST(@Skip AS INT) ROWS FETCH NEXT CAST(@Take AS INT) ROWS ONLY;";

    public async Task<SeasonPassHoldersSummary?> GetSummaryAsync(
        SeasonPassHoldersCriteria criteria,
        CancellationToken cancellationToken)
    {
        return await dbConnection.QuerySingleOrDefaultAsync<SeasonPassHoldersSummary>(
            SummarySql, cancellationToken, FilterParameters(criteria));
    }

    public async Task<IReadOnlyList<SeasonPassHolderRow>> GetPageAsync(
        SeasonPassHoldersCriteria criteria,
        SeasonPassHoldersPaging paging,
        CancellationToken cancellationToken)
    {
        return (await dbConnection.QueryAsync<SeasonPassHolderRow>(
            PageSql,
            cancellationToken,
            new
            {
                criteria.SeasonId,
                NameFilter = EscapeForLike(criteria.NameFilter),
                criteria.AcquiredFromUtc,
                criteria.AcquiredBeforeUtc,
                criteria.MinimumPaid,
                criteria.MaximumPaid,
                SortField = paging.SortField.ToString(),
                SortDescending = paging.SortDirection == SortDirection.Descending,
                paging.Skip,
                paging.Take
            })).ToList();
    }

    /// <remarks>
    /// The date bounds are used exactly as given. Working out where a day starts and ends is the caller's job, because only
    /// the caller knows its time zone.
    /// </remarks>
    private static object FilterParameters(SeasonPassHoldersCriteria criteria) =>
        new
        {
            criteria.SeasonId,
            NameFilter = EscapeForLike(criteria.NameFilter),
            criteria.AcquiredFromUtc,
            criteria.AcquiredBeforeUtc,
            criteria.MinimumPaid,
            criteria.MaximumPaid
        };

    /// <summary>
    /// Escapes the <c>LIKE</c> wildcards so a name containing <c>%</c> or <c>_</c> is searched for literally. The square
    /// bracket has to go first, because the other two replacements introduce brackets.
    /// </summary>
    private static string? EscapeForLike(string? nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter))
            return null;

        return nameFilter
            .Trim()
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }
}
