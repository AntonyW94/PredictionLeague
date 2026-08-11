using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's prize page, as two things rather than one flattened list.
/// </summary>
/// <remarks>
/// The old statement left-joined the prize settings onto the league, so a league with four prizes came back as four
/// copies of its own details and the handler took the first row for the header. A league with none came back as one row
/// of nulls, and the prize columns were nullable throughout to allow for it - so every read of them needed a
/// <c>!</c> to promise the compiler they were really there.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaguePrizesPageData(
    LeaguePrizesHeaderRow Header,
    IReadOnlyList<LeaguePrizeSettingRow> PrizeSettings);
