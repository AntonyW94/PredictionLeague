using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leaderboards;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class ExactScoresLeaderboardEntryDto
{
    public long Rank { get; set; }
    public required string PlayerName { get; init; }
    public int ExactScoresCount { get; init; }
    public required string UserId { get; init; }
}
