namespace PredictionLeague.Contracts.Boosts;

public record UserRoundBoostDto(
    int LeagueId,
    string UserId,
    string BoostCode
);