using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services;

/// <summary>One item and the position it holds, as assigned by <see cref="Ranking"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record Ranked<T>(T Item, int Rank);
