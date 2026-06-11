using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Freezes a league's prize scheme into concrete <see cref="LeaguePrizeSetting"/> rows once the
/// entry deadline has passed. Idempotent: leagues that already have settings are skipped.
/// </summary>
public interface IPrizeSchemeFreezeService
{
    /// <summary>
    /// Attempts to freeze the given league's scheme. Returns true if settings were created and
    /// persisted; false if the league was not eligible (no scheme, deadline not passed, already
    /// frozen, or the scheme produced no prizes).
    /// </summary>
    Task<bool> TryFreezeAsync(League league, CancellationToken cancellationToken);
}
