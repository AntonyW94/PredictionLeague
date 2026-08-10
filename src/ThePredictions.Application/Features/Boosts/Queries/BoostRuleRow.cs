using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>An enabled boost rule for a league, and its per-season allowance.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostRuleRow(
    int LeagueBoostRuleId,
    string BoostCode,
    string Name,
    string? ImageUrl,
    int TotalUsesPerSeason);
