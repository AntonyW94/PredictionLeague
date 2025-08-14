using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public class DefinePrizeSettingDto
{
    public PrizeType PrizeType { get; set; }
    public int Rank { get; set; }
    public decimal PrizeAmount { get; set; }
    public string? PrizeDescription { get; set; }
    public int Multiplier { get; set; } = 1;
}