namespace PredictionLeague.Domain.Models;

public class LeagueRoundResult
{
    public int LeagueId { get; init; }
    public int RoundId { get; init; }
    public string UserId { get; init; } = null!;
    public int BasePoints { get; set; }
    public int BoostedPoints { get; set; }
    public bool HasBoost { get; set; }
    public string? AppliedBoostCode { get; set; }
}