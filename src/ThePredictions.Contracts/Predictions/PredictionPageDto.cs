using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Dashboard;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage]
public class PredictionPageDto
{
    public int RoundId { get; init; }
    public int RoundNumber { get; init; }
    public string? RoundDisplayName { get; init; }
    public string SeasonName { get; init; } = string.Empty;
    public DateTime DeadlineUtc { get; init; }
    public bool IsTournament { get; init; }
    public bool IsLastRoundOfSeason { get; init; }
    public List<MatchPredictionDto> Matches { get; init; } = [];
    public List<PredictionLeagueDto> Leagues { get; init; } = [];
}
