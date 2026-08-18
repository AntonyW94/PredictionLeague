using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One season pass an account holds, and what was paid for it.</summary>
/// <remarks>
/// The source comes back because a pass given as a trial or a freebie is still a pass but is not money spent, and that
/// distinction was a <c>WHERE sp.[Source] = @PurchasedSource</c> inside a sum.
///
/// The season arrives as an id only. Which seasons are still running is a rule about rounds, not about passes, so it is
/// answered once for all seasons rather than restated on every pass row.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserSeasonPassRow(
    string UserId,
    int SeasonId,
    SeasonPassTier Tier,
    SeasonPassSource Source,
    decimal AmountPaid,
    decimal SmsFeePaid,
    DateTime CreatedAtUtc);
