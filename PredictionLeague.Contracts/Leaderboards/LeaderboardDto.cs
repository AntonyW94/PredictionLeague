namespace PredictionLeague.Contracts.Leaderboards;

public class LeaderboardDto
{
    public string LeagueName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public List<LeaderboardEntryDto> Entries { get; init; } = new();
}