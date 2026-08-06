using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage]
public record LeagueRoundBoostUpdate(
    int LeagueId,
    int RoundId,
    string UserId,
    int BoostedPoints,
    bool HasBoost,
    string? AppliedBoostCode
);
