using ThePredictions.Contracts.Dashboard;

namespace ThePredictions.Contracts.Predictions;

public class PredictionPageDto
{
    public int RoundId { get; init; }
    public int RoundNumber { get; init; }
    public string SeasonName { get; init; } = string.Empty;
    public DateTime DeadlineUtc { get; init; }
    public bool IsPastDeadline { get; init; }
    public bool IsTournament { get; init; }
    public bool IsLastRoundOfSeason { get; init; }
    public List<MatchPredictionDto> Matches { get; init; } = [];
    public List<PredictionLeagueDto> Leagues { get; init; } = [];
}