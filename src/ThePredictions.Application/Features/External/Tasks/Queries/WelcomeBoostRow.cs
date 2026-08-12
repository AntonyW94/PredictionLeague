using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>One boost a league offers, as the welcome email describes it.</summary>
/// <remarks>
/// Switched-off rules arrive too: whether a boost is on offer is a rule, and the email must not advertise one nobody can use.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomeBoostRow(
    int RuleId,
    int LeagueId,
    string Name,
    string? Description,
    string? ImageUrl,
    int TotalUsesPerSeason,
    bool IsEnabled);
