namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// Which boost usages a given player is allowed to see: their own always, and anyone else's only once that
/// round's deadline has passed.
///
/// This is a fairness rule, and until August 2026 it lived in a SQL predicate:
///
/// <code>
/// AND (ubu.[UserId] = @CurrentUserId OR r.[DeadlineUtc] &lt;= GETUTCDATE())
/// </code>
///
/// Being SQL made it unreachable from a unit test, and reading the database's clock rather than an injected
/// one meant even an integration test could only arrange deadlines relative to "now" - never pin an instant
/// and check the boundary. Both problems go away here.
///
/// Getting it wrong reveals what an opponent has played while they can still change it, which is the same
/// class of rule as the dashboard's prediction split.
/// </summary>
public static class BoostUsageVisibility
{
    /// <summary>
    /// Filters usages to those <paramref name="currentUserId"/> may see at <paramref name="nowUtc"/>.
    /// </summary>
    public static List<BoostUsageRow> VisibleTo(
        IEnumerable<BoostUsageRow> usages,
        string currentUserId,
        DateTime nowUtc) =>
        usages
            .Where(usage => IsVisibleTo(usage, currentUserId, nowUtc))
            .ToList();

    /// <summary>
    /// Whether one usage is visible. The deadline comparison is inclusive - a round whose deadline is exactly
    /// now has closed, matching the <c>&lt;=</c> the SQL used, so a boost is never hidden a tick longer than
    /// the round it belongs to.
    /// </summary>
    public static bool IsVisibleTo(BoostUsageRow usage, string currentUserId, DateTime nowUtc) =>
        usage.UserId == currentUserId || usage.RoundDeadlineUtc <= nowUtc;
}
