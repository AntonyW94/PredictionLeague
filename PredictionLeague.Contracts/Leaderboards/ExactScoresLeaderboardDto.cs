namespace PredictionLeague.Contracts.Leaderboards;

public class ExactScoresLeaderboardDto
{
    public string LeagueName { get; set; } = null!;
    public string SeasonName { get; set; } = null!;
    public List<ExactScoresLeaderboardEntryDto> Entries { get; set; } = new();
}