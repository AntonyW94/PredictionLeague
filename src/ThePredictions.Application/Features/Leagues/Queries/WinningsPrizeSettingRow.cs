using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prize the league is offering, with the id that ties a win back to it.
/// </summary>
/// <remarks>
/// Not <see cref="LeaguePrizeSettingRow"/>, which the prize page uses: that one carries the rank and no id, because
/// nothing there needs to match a win to the prize it came from. This one needs the id and the administrator's wording,
/// and has no use for the rank.
///
/// <see cref="Name"/> is nullable because <c>PrizeDescription</c> is. The old result type said otherwise.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WinningsPrizeSettingRow(
    int Id,
    PrizeType PrizeType,
    string? Name,
    decimal Amount,
    string? Stage);
