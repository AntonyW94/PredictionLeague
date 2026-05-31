using Ardalis.GuardClauses;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public class SeasonAccessService(
    ISeasonRepository seasonRepository,
    ISeasonPassRepository seasonPassRepository,
    IDateTimeProvider dateTimeProvider) : ISeasonAccessService
{
    public async Task EnsureCanParticipateAsync(string userId, int seasonId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);

        // 1. Already participating this season (any pass exists) -> allow.
        if (await seasonPassRepository.ExistsForUserSeasonAsync(userId, seasonId, cancellationToken))
            return;

        var season = await seasonRepository.GetByIdAsync(seasonId, cancellationToken);
        Guard.Against.EntityNotFound(seasonId, season, nameof(Season));

        // 2. Free season -> record £0 Free participation (burns the free-first-season) and allow.
        if (!season!.RequiresPass)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateFree(userId, seasonId, dateTimeProvider), cancellationToken);
            return;
        }

        // 3. Paid season with no prior records -> grant the one-time free trial and allow.
        var existingRecordCount = await seasonPassRepository.CountForUserAsync(userId, cancellationToken);
        if (existingRecordCount == 0)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateTrial(userId, seasonId, dateTimeProvider), cancellationToken);
            return;
        }

        // 4. Paid season, user already has history -> purchase required.
        throw new SeasonPassRequiredException(seasonId);
    }
}
