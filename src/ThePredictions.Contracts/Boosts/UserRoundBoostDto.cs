using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserRoundBoostDto(
    int LeagueId,
    string UserId,
    string BoostCode
);
