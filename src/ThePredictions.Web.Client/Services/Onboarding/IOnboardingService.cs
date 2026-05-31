using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Web.Client.Services.Onboarding;

public interface IOnboardingService
{
    Task<OnboardingChecklistDto?> GetChecklistAsync();
    Task SkipAsync(string stepKey);
    Task DismissAsync();
}
