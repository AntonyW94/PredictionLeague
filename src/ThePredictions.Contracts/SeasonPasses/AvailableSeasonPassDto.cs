using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public record AvailableSeasonPassDto(
    int SeasonId,
    string SeasonName,
    string? CompetitionLogoUrl,
    bool RequiresPayment,
    decimal? StandardPrice,
    decimal? PremiumPrice,
    bool IsTrialEligible,
    int PlayerCount,
    DateTime? NextEntryDeadlineUtc);
