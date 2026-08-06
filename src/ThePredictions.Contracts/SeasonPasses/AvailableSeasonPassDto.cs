using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
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
