using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Repositories;

/// <summary>
/// Write path for a league's boost configuration (<c>LeagueBoostRules</c> / <c>LeagueBoostWindows</c>).
/// Previously these tables were seed-only; this is the net-new admin write path.
/// </summary>
public interface ILeagueBoostRuleRepository
{
    /// <summary>Whether the league already has any boost rules (used for the write-once gate).</summary>
    Task<bool> HasRulesAsync(int leagueId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the league's boost rules and windows with the supplied selections (enabled boosts only).
    /// Unknown boost codes are ignored.
    /// </summary>
    Task SetRulesAsync(int leagueId, IReadOnlyList<LeagueBoostSelectionDto> selections, CancellationToken cancellationToken);
}
