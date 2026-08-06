using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage]
public record UserRoundBoostDto(
    int LeagueId,
    string UserId,
    string BoostCode
);
