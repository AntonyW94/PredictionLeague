namespace PredictionLeague.Contracts.Boosts;

public record UserRoundBoostDto(
    int LeagueId,
    int RoundId,
    string UserId,
    string BoostCode
);