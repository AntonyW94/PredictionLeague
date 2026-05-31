using Ardalis.GuardClauses;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Services;

public class SeasonAccessService(ISeasonPassRepository seasonPassRepository) : ISeasonAccessService
{
    public async Task EnsureCanParticipateAsync(string userId, int seasonId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);

        // Acquire-first model: a Season Pass is required for EVERY season (free seasons
        // are acquired for £0 via the acquire flow). The gate only checks the user already
        // holds a pass for this season; it does NOT grant one. Acquisition is a deliberate,
        // separate action (AcquireSeasonPassCommand) so users acquire before taking part.
        if (await seasonPassRepository.ExistsForUserSeasonAsync(userId, seasonId, cancellationToken))
            return;

        throw new SeasonPassRequiredException(seasonId);
    }
}
