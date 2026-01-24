using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// DTO for displaying active rounds (upcoming + in-progress) on the dashboard tile.
/// </summary>
public record ActiveRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    RoundStatus Status,
    IEnumerable<ActiveRoundMatchDto> Matches,
    OutcomeSummaryDto? OutcomeSummary);

/// <summary>
/// Summary of prediction outcomes for in-progress rounds.
/// </summary>
public record OutcomeSummaryDto(
    int ExactScoreCount,
    int CorrectResultCount,
    int IncorrectCount);
