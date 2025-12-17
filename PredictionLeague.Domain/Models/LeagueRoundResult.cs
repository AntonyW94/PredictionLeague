namespace PredictionLeague.Domain.Models;

public class LeagueRoundResult
{
    public int LeagueId { get; init; }
    public int RoundId { get; init; }
    public string UserId { get; init; } = null!;
    public int BasePoints { get; init; }
    public int BoostedPoints { get; set; }
    public bool HasBoost { get; init; }
    public string? AppliedBoostCode { get; init; }
    public int ExactScoreCount { get; init; }
}