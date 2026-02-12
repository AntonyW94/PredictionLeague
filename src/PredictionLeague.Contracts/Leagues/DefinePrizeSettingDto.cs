using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public class DefinePrizeSettingDto
{
    public PrizeType PrizeType { get; init; }
    public int Rank { get; init; }
    public decimal PrizeAmount { get; set; }
    public string? PrizeDescription { get; init; }
    public int Multiplier { get; init; } = 1;
}