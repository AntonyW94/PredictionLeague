using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
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
