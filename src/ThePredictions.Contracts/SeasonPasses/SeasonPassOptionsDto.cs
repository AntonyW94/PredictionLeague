namespace ThePredictions.Contracts.SeasonPasses;

public record SeasonPassOptionsDto(
    int SeasonId,
    string SeasonName,
    bool RequiresPayment,
    decimal? StandardPrice,
    decimal? PremiumPrice,
    bool IsTrialEligible,
    bool AlreadyHeld);
