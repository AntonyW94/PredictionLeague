using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

/// <summary>One recurring cost of running the site.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RunningCostRow(
    int Id,
    string Name,
    decimal Amount,
    string Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes);
