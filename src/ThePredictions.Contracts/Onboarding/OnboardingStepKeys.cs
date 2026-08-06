using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Onboarding;

/// <summary>Stable onboarding step keys, shared by the server registry and the client.</summary>
[ExcludeFromCodeCoverage]
public static class OnboardingStepKeys
{
    public const string GetPass = "get-pass";
    public const string JoinLeague = "join-league";
    public const string AddMobile = "add-mobile";
    public const string AddPayoutDetails = "add-payout-details";
}
