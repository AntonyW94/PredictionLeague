using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

/// <summary>
/// Summary of prediction outcomes for in-progress rounds.
/// </summary>
[ExcludeFromCodeCoverage]
public record OutcomeSummaryDto(
    int ExactScoreCount,
    int CorrectResultCount,
    int IncorrectCount);
