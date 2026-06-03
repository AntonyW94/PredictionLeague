namespace ThePredictions.Contracts.Boosts;

/// <summary>The full set of boost selections for a league (replaces any existing selection).</summary>
public class SetLeagueBoostRulesRequest
{
    public List<LeagueBoostSelectionDto> Selections { get; set; } = [];
}
