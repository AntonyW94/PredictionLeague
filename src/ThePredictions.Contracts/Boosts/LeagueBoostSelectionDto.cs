using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

/// <summary>An admin's choice for one boost in a league: whether it is offered, its season cap, and any windows.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class LeagueBoostSelectionDto
{
    public string BoostCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int TotalUsesPerSeason { get; set; }
    public List<BoostWindowSelectionDto> Windows { get; set; } = [];
}
