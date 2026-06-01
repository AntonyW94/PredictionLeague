using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.RunningCosts;

public class SaveRunningCostRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public CostFrequency Frequency { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public string? Notes { get; set; }
}
