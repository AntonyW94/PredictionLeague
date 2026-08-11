using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One match on the dashboard, with the player's own prediction and how everybody's predictions split.
/// </summary>
/// <remarks>
/// The three counts are the split across every player who predicted this match - home win, draw, away win, worked out from the
/// scorelines. They arrive in full: whether the player may <b>see</b> them yet is a rule, and showing them early would reveal
/// what opponents have chosen while there is still time to copy it.
///
/// <see cref="CustomLockTimeUtc"/> is what that rule turns on, and what the round's own deadline is worked out from.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ActiveRoundMatchRow(
    int RoundId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    string? HomeTeamShortName,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome? Outcome,
    MatchStatus Status,
    int? ActualHomeScore,
    int? ActualAwayScore,
    DateTime MatchDateTimeUtc,
    int? MatchNumber,
    bool AreTeamsConfirmed,
    string? PlaceholderHomeName,
    string? PlaceholderAwayName,
    int HomeCount,
    int DrawCount,
    int AwayCount,
    DateTime? CustomLockTimeUtc);
