using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The league and its season, with the three counts its arithmetic needs.
/// </summary>
/// <remarks>
/// <see cref="Price"/>, <see cref="MemberCount"/> and <see cref="PrizeFundOverride"/> arrive separately rather than
/// as a pot: the pot is <c>PrizeFund.Total</c>, which the My Leagues tile also uses. Likewise
/// <see cref="CompletedRoundCount"/> against <see cref="NumberOfRounds"/> is <c>SeasonCompletion.IsFinished</c> - the
/// third and last place that comparison was written out in SQL.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDashboardHeaderRow(
    string Name,
    CompetitionType CompetitionType,
    DateTime SeasonStartDateUtc,
    DateTime? EntryDeadlineUtc,
    decimal Price,
    decimal? PrizeFundOverride,
    bool IsFree,
    int MemberCount,
    int SeasonRoundCount,
    int CompletedRoundCount);
