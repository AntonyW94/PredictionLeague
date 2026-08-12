using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>A prize scheme attached to a league.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeSchemeRow(int Id);
