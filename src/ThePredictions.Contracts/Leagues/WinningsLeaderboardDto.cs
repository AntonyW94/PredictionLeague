using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class WinningsLeaderboardDto
{
    public List<WinningsLeaderboardEntryDto> Entries { get; set; } = [];
}
