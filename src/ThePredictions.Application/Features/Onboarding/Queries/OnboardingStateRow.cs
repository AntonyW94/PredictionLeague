using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Onboarding.Queries;

/// <summary>How far a player has got with the things the checklist asks of them.</summary>
/// <remarks>
/// <see cref="LeagueCount"/> counts every membership whatever its status, including one still waiting to be approved: asking to
/// join is the step, and the checklist should not un-tick itself while an administrator decides.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record OnboardingStateRow(
    int PassCount,
    int LeagueCount,
    string? PhoneNumber,
    bool HasPayoutDetails);
