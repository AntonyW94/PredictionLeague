using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IUserBadgeRepository
{
    /// <summary>
    /// Idempotently awards a badge. Returns true only if a new row was inserted (an existing award is a
    /// no-op), so callers can collect the genuinely new awards - e.g. for the round-results digest.
    /// </summary>
    Task<bool> AwardAsync(AwardedBadge badge, CancellationToken cancellationToken);
}
