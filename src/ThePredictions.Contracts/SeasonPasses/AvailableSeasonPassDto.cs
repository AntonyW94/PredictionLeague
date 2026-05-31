namespace ThePredictions.Contracts.SeasonPasses;

public record AvailableSeasonPassDto(
    int SeasonId,
    string SeasonName,
    bool RequiresPayment,
    decimal? StandardPrice,
    decimal? PremiumPrice,
    bool IsTrialEligible);
