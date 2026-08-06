using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

/// <summary>The full set of boost selections for a league (replaces any existing selection).</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class SetLeagueBoostRulesRequest
{
    public List<LeagueBoostSelectionDto> Selections { get; set; } = [];
}
