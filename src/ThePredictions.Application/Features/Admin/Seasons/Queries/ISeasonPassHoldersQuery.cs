using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>
/// Reads one page of a season's pass holders, and the totals for everybody the filters match.
/// </summary>
/// <remarks>
/// The one read in this refactor where filtering, sorting and paging stay in the database on purpose. Choosing which rows
/// to return is fetching, and a page cannot be taken without sorting first, so the two belong in the same place. What
/// moves is the reverse of everywhere else: the escaping of the name filter's wildcards is <c>LIKE</c> syntax, so it
/// belonged to the adapter rather than to the handler that was doing it.
/// </remarks>
public interface ISeasonPassHoldersQuery
{
    /// <summary>The season's name and the totals across every matching holder, or nothing if there is no such season.</summary>
    Task<SeasonPassHoldersSummary?> GetSummaryAsync(SeasonPassHoldersCriteria criteria, CancellationToken cancellationToken);

    /// <summary>One page of matching holders, in the order asked for.</summary>
    Task<IReadOnlyList<SeasonPassHolderRow>> GetPageAsync(
        SeasonPassHoldersCriteria criteria,
        SeasonPassHoldersPaging paging,
        CancellationToken cancellationToken);
}
