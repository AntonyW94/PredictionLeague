namespace PredictionLeague.Contracts.Leagues;

public class PredictionResultDto
{
    public string UserId { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public bool HasPredicted { get; init; }
    public int TotalPoints { get; init; }
    public long Rank { get; init; }
    public List<PredictionScoreDto> Predictions { get; init; } = new();
}