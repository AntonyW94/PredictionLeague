namespace PredictionLeague.Domain.Models;

public class RoundResult
{
    public int Id { get; init; }
    public int RoundId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public int TotalPoints { get; init; }
}