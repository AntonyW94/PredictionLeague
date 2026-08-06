using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class WinningsLeaderboardDto
{
    public List<WinningsLeaderboardEntryDto> Entries { get; set; } = [];
}
