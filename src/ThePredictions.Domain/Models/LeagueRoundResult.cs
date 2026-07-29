using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

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

    [ExcludeFromCodeCoverage]
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

    /// <remarks>
    /// Every boost here must be a <b>multiplier</b> of <see cref="BasePoints"/>, never an addition or a
    /// flat override. <c>LeagueStatsRepository</c> recovers the multiplier for a member's round as
    /// <c>BoostedPoints / BasePoints</c> so it can apply the same boost to a subtotal of the round
    /// (finished matches only) without restating the rule in SQL. An additive boost would make that
    /// ratio meaningless and would silently corrupt the round change arrow.
    /// <c>ApplyBoost_ShouldProduceAnExactMultipleOfBasePoints_ForEveryBoostCode</c> pins this.
    /// </remarks>
    public void ApplyBoost(string boostCode)
    {
        BoostedPoints = boostCode switch
        {
            "DoubleUp" => BasePoints * 2,
            _ => BasePoints
        };
    }
}