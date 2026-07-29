namespace ThePredictions.Application.Repositories;

/// <summary>
/// Maintains the cached per-member ranks in <c>[LeagueMemberStats]</c> that the My Leagues tile reads.
/// </summary>
/// <remarks>
/// Both methods run the same deterministic recompute, differing only in scope. The recompute derives
/// every cached rank from the current results plus the league's active round, so it is idempotent and
/// order-independent - calling it twice, or calling it late, yields the same values. That is what makes
/// it safe to hang off any number of triggers.
/// </remarks>
public interface ILeagueStatsRepository
{
    /// <summary>
    /// Recomputes the cached ranks for a single league. Call this whenever the league's approved
    /// membership changes, because every member's rank is relative to the others.
    /// </summary>
    Task RefreshLeagueAsync(int leagueId, CancellationToken cancellationToken);

    /// <summary>
    /// Recomputes the cached ranks for every league in a season. Call this whenever results change.
    /// </summary>
    Task RefreshSeasonAsync(int seasonId, CancellationToken cancellationToken);
}
