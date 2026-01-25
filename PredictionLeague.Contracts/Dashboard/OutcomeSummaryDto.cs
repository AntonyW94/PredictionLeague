namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Summary of prediction outcomes for in-progress rounds.
/// </summary>
public record OutcomeSummaryDto(
    int ExactScoreCount,
    int CorrectResultCount,
    int IncorrectCount);
