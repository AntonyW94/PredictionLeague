using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Dashboard;

/// <summary>
/// DTO for displaying active rounds (upcoming + in-progress) on the dashboard tile.
/// </summary>
/// <remarks>
/// <see cref="DeadlineUtc"/> is the round deadline (the earliest lock). <see cref="LatestPredictionDeadlineUtc"/>
/// is the latest point at which any match can still be predicted, honouring per-match custom lock times, so a
/// combined round stays actionable for its later matches after the round deadline has passed.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record ActiveRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    string? RoundDisplayName,
    bool IsTournament,
    DateTime DeadlineUtc,
    DateTime LatestPredictionDeadlineUtc,
    bool HasUserPredicted,
    RoundStatus Status,
    IEnumerable<ActiveRoundMatchDto> Matches,
    OutcomeSummaryDto? OutcomeSummary);
