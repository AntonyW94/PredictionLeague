using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// A round that might belong on the player's dashboard.
/// </summary>
/// <remarks>
/// <see cref="HasConfirmedMatch"/> is answered in the adapter rather than worked out from the returned matches, and
/// deliberately: the old statement's <c>EXISTS</c> looked at <b>every</b> match including postponed ones, while the matches
/// returned alongside this exclude them. Computing it from those rows would quietly drop a round whose only confirmed match had
/// been called off.
///
/// <see cref="LatestPredictionDeadlineUtc"/> is <b>not</b> here. It used to be a <c>COALESCE</c> over a correlated <c>MAX</c>,
/// and it is now <c>PredictionWindow.LatestDeadline</c> over the matches - which is also what decides whether this round
/// appears at all.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ActiveRoundCandidateRow(
    int RoundId,
    string SeasonName,
    int RoundNumber,
    string? RoundDisplayName,
    DateTime DeadlineUtc,
    RoundStatus Status,
    CompetitionType CompetitionType,
    bool HasUserPredicted,
    bool HasConfirmedMatch);
