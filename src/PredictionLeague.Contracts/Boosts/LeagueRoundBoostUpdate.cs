namespace PredictionLeague.Contracts.Boosts;

public record LeagueRoundBoostUpdate(
    int LeagueId,
    int RoundId,
    string UserId,
    int BoostedPoints,
    bool HasBoost,
    string? AppliedBoostCode
);