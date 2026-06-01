namespace ThePredictions.Application.Features.Onboarding;

/// <summary>The live data the onboarding steps' completion is derived from.</summary>
public record OnboardingUserState(int PassCount, int LeagueCount, bool HasMobile, bool HasPayoutDetails);
