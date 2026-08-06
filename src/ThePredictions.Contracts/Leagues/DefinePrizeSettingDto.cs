using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class DefinePrizeSettingDto
{
    public PrizeType PrizeType { get; init; }
    public int Rank { get; init; }
    public decimal PrizeAmount { get; set; }
    public string? PrizeDescription { get; init; }
    public int Multiplier { get; init; } = 1;
}
