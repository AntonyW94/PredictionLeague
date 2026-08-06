using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SeasonPassOptionsDto(
    int SeasonId,
    string SeasonName,
    string? CompetitionLogoUrl,
    string? CompetitionDescription,
    bool RequiresPayment,
    decimal? StandardPrice,
    decimal? PremiumPrice,
    bool IsTrialEligible,
    bool AlreadyHeld,
    bool EntryOpen,
    int PlayerCount,
    DateTime? NextEntryDeadlineUtc);
