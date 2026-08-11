using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prize won in the league, and what it was won for.
/// </summary>
/// <remarks>
/// <see cref="RoundNumber"/> and <see cref="Month"/> are each set only for the kind of prize that has one, which is why
/// both are nullable rather than one being a general "period" field: a round prize has no month and a monthly prize has no
/// round.
///
/// Name parts, not a formatted name - the winnings page shows the abbreviated form, unlike the payouts screen.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WinningsRow(
    decimal Amount,
    int LeaguePrizeSettingId,
    PrizeType PrizeType,
    string FirstName,
    string LastName,
    int? RoundNumber,
    int? Month,
    string UserId);
