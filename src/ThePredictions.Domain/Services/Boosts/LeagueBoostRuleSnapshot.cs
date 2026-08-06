using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class LeagueBoostRuleSnapshot
{
    public bool IsEnabled { get; init; }
    public int TotalUsesPerSeason { get; init; }
    public IReadOnlyList<BoostWindowSnapshot> Windows { get; init; } = new List<BoostWindowSnapshot>();
}
