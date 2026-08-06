using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueRoundBoostUpdate(
    int LeagueId,
    int RoundId,
    string UserId,
    int BoostedPoints,
    bool HasBoost,
    string? AppliedBoostCode
);
