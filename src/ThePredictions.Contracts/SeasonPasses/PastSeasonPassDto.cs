using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record PastSeasonPassDto(
    int SeasonId,
    string SeasonName,
    string? CompetitionLogoUrl,
    int PlayerCount);
