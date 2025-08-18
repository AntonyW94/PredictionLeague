using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Contracts.Leaderboards;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ExactScoresLeaderboardEntryDto
{
    public long Rank { get; set; }
    public required string PlayerName { get; init; }
    public int ExactScoresCount { get; init; }
    public required string UserId { get; init; }
}