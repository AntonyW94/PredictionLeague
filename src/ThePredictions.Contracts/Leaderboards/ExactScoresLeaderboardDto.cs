using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leaderboards;

[ExcludeFromCodeCoverage]
public class ExactScoresLeaderboardDto
{
    public List<ExactScoresLeaderboardEntryDto> Entries { get; init; } = [];
}
