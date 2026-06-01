using ThePredictions.Domain.Common.Pricing;

namespace ThePredictions.Application.Services;

public interface ISeasonPriceRecommendationService
{
    /// <summary>
    /// Computes a recommended Standard Season Pass price for a season draft, using the business-borne
    /// running costs, length-weighted apportionment across paid seasons, and the last comparable
    /// season's player count. Always advisory - the admin can override it.
    /// </summary>
    /// <param name="seasonId">The season being edited, excluded from horizon/comparable lookups; null when creating.</param>
    Task<PriceRecommendation> RecommendAsync(
        int competitionId,
        int numberOfRounds,
        DateTime startDateUtc,
        int? seasonId,
        CancellationToken cancellationToken);
}
