using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage]
public class PredictionLeagueDto
{
    public int LeagueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool HasBoosts { get; init; }
    public bool HasUnusedBoostThisSeason { get; init; }
    public string? SelectedBoostCode { get; init; }
}
