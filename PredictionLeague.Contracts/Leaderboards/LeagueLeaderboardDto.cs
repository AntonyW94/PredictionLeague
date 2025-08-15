namespace PredictionLeague.Contracts.Leaderboards;

public class LeagueLeaderboardDto
{
    public int LeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public IEnumerable<LeaderboardEntryDto> Entries { get; set; } = new List<LeaderboardEntryDto>();
}