using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Dashboard;

/// <summary>
/// DTO for displaying active rounds (upcoming + in-progress) on the dashboard tile.
/// </summary>
public record ActiveRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    string? RoundDisplayName,
    bool IsTournament,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    RoundStatus Status,
    IEnumerable<ActiveRoundMatchDto> Matches,
    OutcomeSummaryDto? OutcomeSummary);
