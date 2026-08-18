using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One prize an account has won.</summary>
/// <remarks>
/// The scope of the prize arrives in the three columns that actually store it - <see cref="Stage"/> for a tournament
/// stage, <see cref="RoundNumber"/> for a round, <see cref="Month"/> for a month - and never more than one of them is
/// set. Turning those into a title is a rule, so it is not done here.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserWinningRow(
    string UserId,
    int LeagueId,
    string LeagueName,
    int SeasonId,
    PrizeType PrizeType,
    string? Stage,
    int? RoundNumber,
    int? Month,
    decimal Amount,
    DateTime AwardedDateUtc);
