namespace PredictionLeague.Contracts.Leaderboards;

public record LeaderboardEntryDto(
    int Rank,
    string PlayerName,
    int TotalPoints
);
