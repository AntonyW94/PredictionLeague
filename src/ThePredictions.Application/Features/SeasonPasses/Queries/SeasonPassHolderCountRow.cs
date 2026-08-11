using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>How many players are taking part in one season.</summary>
/// <remarks>
/// Counted from the passes rather than from league membership, and deliberately: a pass is written for every kind of
/// participation - purchase, trial or free season - and there is one per player per season, so it counts a player who has
/// not picked a league yet exactly once. A count over a scoped set with no classification in it, so it stays in the read.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassHolderCountRow(int SeasonId, int HolderCount);
