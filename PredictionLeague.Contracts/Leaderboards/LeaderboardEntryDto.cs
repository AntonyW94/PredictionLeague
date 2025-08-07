namespace PredictionLeague.Contracts.Leaderboards;

public record LeaderboardEntryDto(
    long Rank,
    string PlayerName,
    int? TotalPoints
);
