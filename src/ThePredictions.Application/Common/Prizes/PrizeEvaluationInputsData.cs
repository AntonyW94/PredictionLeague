using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>What <see cref="IPrizeEvaluationInputsQuery"/> returns for one league.</summary>
/// <remarks>
/// The scheme arrives as a list rather than a flag: whether the league has one is <c>Schemes.Count &gt; 0</c>, which is a
/// judgement the caller makes. The entries are read whether or not there is a scheme, because with none there are none - which
/// removes the conditional second trip the old code made.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeEvaluationInputsData(
    PrizeLeagueRow League,
    IReadOnlyList<PrizeSchemeRow> Schemes,
    IReadOnlyList<PrizeSchemeEntryRow> Entries);
