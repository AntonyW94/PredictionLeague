using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class PrizeDto
{
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Winner { get; init; }
    public string? UserId { get; init; }
}
