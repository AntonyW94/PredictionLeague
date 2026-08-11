using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// What one player scored in one round of one league, with everything the records need to interpret it.
/// </summary>
/// <remarks>
/// <see cref="HasAnyPrediction"/> answers the old <c>EXISTS</c> subquery in the lowest-round block. Whether a
/// player entered a round is a fact about the data; that a round nobody entered cannot be held against them is
/// the rule, and it moved. Without it the worst-round record would go to whoever had most recently joined,
/// scoring zero for rounds that closed before they arrived.
///
/// <see cref="RoundStatus"/> and <see cref="RoundStartDateUtc"/> are here for the wins counts: only completed
/// rounds count towards them, and the month a round belongs to is the calendar month it started in.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRecordRoundScoreRow(
    string UserId,
    string FirstName,
    string LastName,
    int RoundId,
    int RoundNumber,
    DateTime RoundStartDateUtc,
    RoundStatus RoundStatus,
    int BoostedPoints,
    bool HasAnyPrediction);
