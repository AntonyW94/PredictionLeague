using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.RunningCosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record RunningCostDto(
    int Id,
    string Name,
    decimal Amount,
    string Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes);
