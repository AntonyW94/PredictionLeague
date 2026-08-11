using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

/// <summary>The stored pricing settings. The id comes back because choosing between rows is a rule.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PricingSettingsRow(int Id, decimal BufferRate, decimal MinimumFloor);
