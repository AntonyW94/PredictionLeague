using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The league and season facts the winnings page needs.
/// </summary>
/// <remarks>
/// <see cref="PrizeFundOverride"/> is returned and <b>not</b> used. This page works its prize pot out as
/// <c>EntryCount * EntryCost</c>, which is the one place on the site that ignores the administrator's top-up - the three
/// others go through <c>PrizeFund.Total</c>. Returning it makes the difference visible in one place and switching a
/// one-line change; the plan document records the question.
///
/// <see cref="EntryDeadlineUtc"/> is nullable because the column is. The old result type declared it non-nullable, which
/// mattered more here than elsewhere: the deadline is compared against the clock to decide whether prizes have been
/// worked out at all.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WinningsHeaderRow(
    DateTime? EntryDeadlineUtc,
    decimal EntryCost,
    int EntryCount,
    decimal? PrizeFundOverride,
    DateTime SeasonStartDateUtc,
    DateTime SeasonEndDateUtc,
    int TotalRoundsInSeason);
