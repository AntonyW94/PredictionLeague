using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetSeasonPassHoldersQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonPassHoldersQuery, SeasonPassHoldersPageDto?>
{
    /// <summary>
    /// The column filters, shared by the summary and the page read so the two can never disagree
    /// about what "matching" means. Every filter is skipped when its parameter is NULL, which keeps
    /// the SQL a compile-time constant - the schema check cannot resolve SQL built at run time.
    /// </summary>
    private const string ColumnFilters = @"
                AND (@NameFilter IS NULL OR (u.[FirstName] + ' ' + u.[LastName]) LIKE '%' + @NameFilter + '%')
                AND (@AcquiredFromUtc IS NULL OR sp.[CreatedAtUtc] >= @AcquiredFromUtc)
                AND (@AcquiredBeforeUtc IS NULL OR sp.[CreatedAtUtc] < @AcquiredBeforeUtc)
                AND (@MinimumPaid IS NULL OR (sp.[AmountPaid] + sp.[SmsFeePaid]) >= @MinimumPaid)
                AND (@MaximumPaid IS NULL OR (sp.[AmountPaid] + sp.[SmsFeePaid]) <= @MaximumPaid)";

    private const string SummarySql = @"
        SELECT
            s.[Name] AS SeasonName,
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
    /// The sort column is chosen by a parameter rather than by concatenating one in, so there is
    /// no way for a caller to reach the ORDER BY and the SQL stays constant. Every branch but the
    /// chosen one evaluates to NULL for every row, contributing nothing to the ordering.
    /// <para>
    /// OFFSET and FETCH NEXT cast their parameters because the schema check has to infer parameter
    /// types from the call site, and a row count it infers as anything but an integer fails to
    /// compile - which would skip this read instead of verifying it.
    /// </para>
    /// </summary>
    private const string PageSql = @"
        SELECT
            sp.[UserId],
            u.[FirstName] + ' ' + u.[LastName] AS FullName,
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

    public async Task<SeasonPassHoldersPageDto?> Handle(GetSeasonPassHoldersQuery request, CancellationToken cancellationToken)
    {
        var pageSize = PageSizes.Clamp(request.PageSize);
        var nameFilter = BuildNameFilter(request.NameFilter);
        var acquiredFromUtc = request.AcquiredFromUtc?.Date;
        var acquiredBeforeUtc = request.AcquiredToUtc?.Date.AddDays(1);

        var summary = await dbConnection.QuerySingleOrDefaultAsync<SeasonPassHoldersSummaryQueryResult>(
            SummarySql,
            cancellationToken,
            new
            {
                request.SeasonId,
                NameFilter = nameFilter,
                AcquiredFromUtc = acquiredFromUtc,
                AcquiredBeforeUtc = acquiredBeforeUtc,
                request.MinimumPaid,
                request.MaximumPaid
            });

        if (summary is null)
            return null;

        if (summary.MatchingCount == 0)
            return new SeasonPassHoldersPageDto(summary.SeasonName, 0, PagedResult<SeasonPassHolderDto>.Empty(pageSize));

        var page = ClampPage(request.Page, summary.MatchingCount, pageSize);

        var holders = await dbConnection.QueryAsync<SeasonPassHolderQueryResult>(
            PageSql,
            cancellationToken,
            new
            {
                request.SeasonId,
                NameFilter = nameFilter,
                AcquiredFromUtc = acquiredFromUtc,
                AcquiredBeforeUtc = acquiredBeforeUtc,
                request.MinimumPaid,
                request.MaximumPaid,
                SortField = request.SortField.ToString(),
                SortDescending = request.SortDirection == SortDirection.Descending,
                Skip = (page - 1) * pageSize,
                Take = pageSize
            });

        var items = holders
            .Select(h => new SeasonPassHolderDto(
                h.UserId,
                h.FullName,
                h.Email,
                h.Tier,
                h.Source,
                h.AmountPaid,
                h.SmsFeePaid,
                h.CreatedAtUtc))
            .ToList();

        return new SeasonPassHoldersPageDto(
            summary.SeasonName,
            summary.TotalCollected,
            new PagedResult<SeasonPassHolderDto>(items, page, pageSize, summary.MatchingCount));
    }

    /// <summary>
    /// Keeps a stale or hand-typed page number in range, so tightening a filter lands on the last
    /// page of the smaller result set rather than on an empty one.
    /// </summary>
    private static int ClampPage(int requestedPage, int matchingCount, int pageSize)
    {
        var lastPage = (int)Math.Ceiling(matchingCount / (double)pageSize);

        return Math.Clamp(requestedPage, 1, lastPage);
    }

    /// <summary>
    /// Escapes the LIKE wildcards so a name containing % or _ is searched for literally. The
    /// square bracket has to go first, because the other two replacements introduce brackets.
    /// </summary>
    private static string? BuildNameFilter(string? nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter))
            return null;

        return nameFilter
            .Trim()
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }

    /// <summary>
    /// These two are internal rather than private only so the handler's paging, sorting and filter
    /// shaping can be tested - none of it is observable until the reads return something. They stay
    /// nested here so the positional coupling to the SELECTs above never leaves this file.
    /// </summary>
    internal record SeasonPassHoldersSummaryQueryResult(
        string SeasonName,
        int MatchingCount,
        decimal TotalCollected);

    /// <inheritdoc cref="SeasonPassHoldersSummaryQueryResult"/>
    internal record SeasonPassHolderQueryResult(
        string UserId,
        string FullName,
        string Email,
        SeasonPassTier Tier,
        SeasonPassSource Source,
        decimal AmountPaid,
        decimal SmsFeePaid,
        DateTime CreatedAtUtc);
}
