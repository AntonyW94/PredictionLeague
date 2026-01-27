using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Contracts.Leaderboards;

public class LeagueLeaderboardDto
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int LeagueId { get; init; }
    public string LeagueName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public IEnumerable<LeaderboardEntryDto> Entries { get; init; } = new List<LeaderboardEntryDto>();
}