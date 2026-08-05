using System.Text.Json.Serialization;

namespace ThePredictions.Contracts.Common;

/// <summary>
/// One page of a larger result set, carrying enough context for a pager to render itself.
/// <para>
/// <paramref name="TotalCount"/> is the size of the whole matching set (after any filtering),
/// not the size of this page, so a pager can work out how many pages there are.
/// </para>
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Always at least 1, so an empty result still reads as "page 1 of 1".</summary>
    [JsonIgnore]
    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    [JsonIgnore]
    public bool HasPreviousPage => Page > 1;

    [JsonIgnore]
    public bool HasNextPage => Page < TotalPages;

    /// <summary>1-based number of the first item on this page, or 0 when there are none.</summary>
    [JsonIgnore]
    public int FirstItemNumber => TotalCount <= 0 ? 0 : ((Page - 1) * PageSize) + 1;

    /// <summary>1-based number of the last item on this page, or 0 when there are none.</summary>
    [JsonIgnore]
    public int LastItemNumber => TotalCount <= 0 ? 0 : Math.Min(Page * PageSize, TotalCount);

    public static PagedResult<T> Empty(int pageSize) => new([], 1, pageSize, 0);
}
