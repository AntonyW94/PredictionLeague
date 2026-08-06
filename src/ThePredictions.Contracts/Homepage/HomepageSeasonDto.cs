using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Homepage;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record HomepageSeasonDto(
    int Id,
    string Name,
    CompetitionType CompetitionType,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsInProgress,
    bool IsUpcoming,
    int LeagueCount,
    int PlayerCount,
    decimal TotalPrizeFund
);
