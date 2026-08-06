using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

/// <summary>
/// Summary of prediction outcomes for in-progress rounds.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record OutcomeSummaryDto(
    int ExactScoreCount,
    int CorrectResultCount,
    int IncorrectCount);
