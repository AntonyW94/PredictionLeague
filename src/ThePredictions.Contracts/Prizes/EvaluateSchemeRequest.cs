namespace ThePredictions.Contracts.Prizes;

/// <summary>A draft scheme plus the context needed to preview its derived prizes in the editor.</summary>
public class EvaluateSchemeRequest
{
    public int SeasonId { get; set; }
    public decimal Price { get; set; }
    public int EntrantCount { get; set; }
    public PrizeSchemeRequest Scheme { get; set; } = new();
}
