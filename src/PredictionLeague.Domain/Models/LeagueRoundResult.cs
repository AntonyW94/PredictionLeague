namespace PredictionLeague.Domain.Models;

public class LeagueRoundResult
{
    public int LeagueId { get; init; }
    public int RoundId { get; init; }
    public string UserId { get; init; } = null!;
    public int BasePoints { get; init; }
    public int BoostedPoints { get; private set; }
    public bool HasBoost { get; init; }
    public string? AppliedBoostCode { get; init; }
    public int ExactScoreCount { get; init; }

    public LeagueRoundResult() { }

    public LeagueRoundResult(int leagueId, int roundId, string userId, int basePoints, int boostedPoints, bool hasBoost, string? appliedBoostCode, int exactScoreCount)
    {
        LeagueId = leagueId;
        RoundId = roundId;
        UserId = userId;
        BasePoints = basePoints;
        BoostedPoints = boostedPoints;
        HasBoost = hasBoost;
        AppliedBoostCode = appliedBoostCode;
        ExactScoreCount = exactScoreCount;
    }

    public void ApplyBoost(string boostCode)
    {
        BoostedPoints = boostCode switch
        {
            "DoubleUp" => BasePoints * 2,
            _ => BasePoints
        };
    }
}