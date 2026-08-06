using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class PrizeDto
{
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Winner { get; init; }
    public string? UserId { get; init; }
}
