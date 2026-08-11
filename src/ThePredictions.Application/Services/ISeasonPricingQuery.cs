namespace ThePredictions.Application.Services;

/// <summary>
/// Reads what the price recommendation needs about seasons: their length, their dates, whether they are paid, and how many
/// players took part in one of them.
/// </summary>
/// <remarks>
/// The other three reads this calculation needs - the pricing settings, the payment provider's fee and the running costs -
/// are the administrator's own reads, so they go through those ports rather than being repeated here.
/// </remarks>
public interface ISeasonPricingQuery
{
    /// <summary>Every season, with what the pricing horizon and the comparable-season rules need to judge them.</summary>
    Task<IReadOnlyList<SeasonPricingRow>> GetSeasonsAsync(CancellationToken cancellationToken);

    /// <summary>How many different players were approved members of a league in the given season.</summary>
    Task<int> CountApprovedParticipantsAsync(int seasonId, CancellationToken cancellationToken);
}
