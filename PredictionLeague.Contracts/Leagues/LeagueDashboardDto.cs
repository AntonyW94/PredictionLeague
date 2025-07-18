using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Contracts.Leagues;

public class LeagueDashboardDto
{
    public string LeagueName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public List<LeaderboardEntryDto> Entries { get; init; } = new();
}