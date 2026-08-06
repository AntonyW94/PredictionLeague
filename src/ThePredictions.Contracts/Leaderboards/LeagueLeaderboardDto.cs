using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leaderboards;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueLeaderboardDto
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public int LeagueId { get; init; }
    public string LeagueName { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public bool IsFinished { get; init; }
    public bool IsArchivedByUser { get; init; }
    public IEnumerable<LeaderboardEntryDto> Entries { get; init; } = new List<LeaderboardEntryDto>();
}
