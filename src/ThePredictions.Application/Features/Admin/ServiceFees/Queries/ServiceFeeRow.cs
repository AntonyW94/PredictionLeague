using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Queries;

/// <summary>What one payment provider charges.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ServiceFeeRow(string Provider, decimal PercentFee, decimal FixedFee);
