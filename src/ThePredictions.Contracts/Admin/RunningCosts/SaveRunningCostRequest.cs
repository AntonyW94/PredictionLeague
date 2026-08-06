using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.RunningCosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class SaveRunningCostRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public CostFrequency Frequency { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public string? Notes { get; set; }
}
