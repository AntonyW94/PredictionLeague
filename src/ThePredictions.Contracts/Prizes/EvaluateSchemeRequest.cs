using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Prizes;

/// <summary>A draft scheme plus the context needed to preview its derived prizes in the editor.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class EvaluateSchemeRequest
{
    public int SeasonId { get; set; }
    public decimal Price { get; set; }

    /// <summary>Admin money on top of entry fees (added to the pot), for the live preview.</summary>
    public decimal? PrizeFundOverride { get; set; }

    public int EntrantCount { get; set; }
    public PrizeSchemeRequest Scheme { get; set; } = new();
}
