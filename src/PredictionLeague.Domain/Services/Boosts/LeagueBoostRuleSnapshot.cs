using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Services.Boosts;

[ExcludeFromCodeCoverage]
public sealed class LeagueBoostRuleSnapshot
{
    public bool IsEnabled { get; init; }
    public int TotalUsesPerSeason { get; init; }
    public IReadOnlyList<BoostWindowSnapshot> Windows { get; init; } = new List<BoostWindowSnapshot>();
}