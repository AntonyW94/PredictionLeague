using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class SeasonRecapDto
{
    public bool IsFree { get; init; }
    public decimal LeaguePrice { get; init; }

    public int FinalPosition { get; init; }
    public int TotalMembers { get; init; }
    public decimal TotalWinnings { get; init; }
    public decimal ProfitLoss { get; init; }

    public decimal AveragePointsPerRound { get; init; }
    public int BestRoundPoints { get; init; }
    public int? BestRoundNumber { get; init; }
    public int WorstRoundPoints { get; init; }
    public int? WorstRoundNumber { get; init; }
    public int TotalExactScores { get; init; }
    public int RoundsWon { get; init; }
    public int MonthsWon { get; init; }

    public int HighestPosition { get; init; }
    public int RoundsAtHighestPosition { get; init; }
}
