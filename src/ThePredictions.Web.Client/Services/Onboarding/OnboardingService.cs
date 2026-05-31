using System.Net.Http.Json;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Web.Client.Services.Onboarding;

public class OnboardingService(HttpClient httpClient) : IOnboardingService
{
    public async Task<OnboardingChecklistDto?> GetChecklistAsync()
    {
        return await httpClient.GetFromJsonAsync<OnboardingChecklistDto>("api/onboarding");
    }

    public async Task SkipAsync(string stepKey)
    {
        await httpClient.PostAsync($"api/onboarding/skip/{stepKey}", null);
    }

    public async Task DismissAsync()
    {
        await httpClient.PostAsync("api/onboarding/dismiss", null);
    }
}
