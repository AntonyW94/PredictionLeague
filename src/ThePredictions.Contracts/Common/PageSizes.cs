namespace ThePredictions.Contracts.Common;

/// <summary>
/// The page sizes a paged list is allowed to use. Shared so the dropdown the user picks from
/// and the server-side clamp can never disagree.
/// </summary>
public static class PageSizes
{
    public const int Default = 25;

    public static readonly IReadOnlyList<int> Allowed = [5, 10, 25, 50, 100];

    /// <summary>Snaps an incoming page size to the nearest allowed value, falling back to the default.</summary>
    public static int Clamp(int pageSize) => Allowed.Contains(pageSize) ? pageSize : Default;
}
