using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>The lowest and highest round number in the league's season.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostRoundRangeRow(int MinRoundNumber, int MaxRoundNumber);
