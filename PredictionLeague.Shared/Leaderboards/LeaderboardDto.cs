namespace PredictionLeague.Shared.Leaderboards;

public class LeaderboardDto
{
    public string LeagueName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public List<LeaderboardEntryDto> Entries { get; set; } = new();
}