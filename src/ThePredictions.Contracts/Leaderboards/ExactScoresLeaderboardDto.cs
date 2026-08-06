using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leaderboards;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class ExactScoresLeaderboardDto
{
    public List<ExactScoresLeaderboardEntryDto> Entries { get; init; } = [];
}
