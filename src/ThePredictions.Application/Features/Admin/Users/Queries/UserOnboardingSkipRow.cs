using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One onboarding step an account has dismissed.</summary>
/// <remarks>
/// Dismissing a step is not the same as finishing it, and it does not stop it being finished later: three dev accounts
/// dismissed the payout-details prompt and then added payout details anyway. Which of the two wins is the step registry
/// to settle, not this read.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserOnboardingSkipRow(string UserId, string StepKey);
