using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public class PrizeDto
{
    public PrizeType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Winner { get; set; }
    public string? UserId { get; set; }
    
}