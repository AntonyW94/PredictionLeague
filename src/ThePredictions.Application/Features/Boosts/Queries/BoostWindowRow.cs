using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>A configured window on a boost rule: a round range and how many uses it allows.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostWindowRow(
    int LeagueBoostRuleId,
    int StartRoundNumber,
    int EndRoundNumber,
    int MaxUsesInWindow);
