using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Contracts.Predictions;

public class PredictionPageDto
{
    public int RoundId { get; init; }
    public int RoundNumber { get; init; }
    public string SeasonName { get; init; } = string.Empty;
    public DateTime Deadline { get; init; }
    public bool IsPastDeadline { get; init; }
    public List<MatchPredictionDto> Matches { get; init; } = [];
}