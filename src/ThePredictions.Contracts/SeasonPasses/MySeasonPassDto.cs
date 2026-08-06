using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public record MySeasonPassDto(
    int SeasonId,
    string SeasonName,
    string? CompetitionLogoUrl,
    string Tier,
    string Source,
    decimal AmountPaid,
    bool HasSmsReminders,
    DateTime CreatedAtUtc);
