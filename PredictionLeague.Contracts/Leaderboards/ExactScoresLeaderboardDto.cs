namespace PredictionLeague.Contracts.Leaderboards;

public class ExactScoresLeaderboardDto
{
    public List<ExactScoresLeaderboardEntryDto> Entries { get; set; } = new();
}