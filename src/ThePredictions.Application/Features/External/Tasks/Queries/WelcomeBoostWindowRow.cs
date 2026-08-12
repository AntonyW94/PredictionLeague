using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>One stretch of rounds in which a boost may be used, and how often within it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomeBoostWindowRow(
    int LeagueBoostRuleId,
    int StartRoundNumber,
    int EndRoundNumber,
    int MaxUsesInWindow);
