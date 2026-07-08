using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using ThePredictions.Web.Client.Authentication;
using ThePredictions.Web.Client.Services.Boosts;
using ThePredictions.Web.Client.Services.Browser;
using ThePredictions.Web.Client.Services.Consent;
using ThePredictions.Web.Client.Services.Dashboard;
using ThePredictions.Web.Client.Services.EmailSettings;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Services.Live;
using ThePredictions.Web.Client.Services.Onboarding;
using ThePredictions.Web.Client.Services.Payouts;
using ThePredictions.Web.Client.Services.PricingSettings;
using ThePredictions.Web.Client.Services.RunningCosts;
using ThePredictions.Web.Client.Services.SeasonPasses;
using ThePredictions.Web.Client.Services.Theme;
using ThePredictions.Web.Client.ViewModels.Admin.Rounds;

namespace ThePredictions.Web.Client;

public static class DependencyInjection
{
    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddBlazoredLocalStorage();
        services.AddTransient<CookieHandler>();
        services.AddTransient<AuthorizationMessageHandler>();

        services.AddScoped<SessionState>();
        services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ILeagueService, LeagueService>();
        services.AddScoped<ISeasonPassService, SeasonPassService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IRunningCostService, RunningCostService>();
        services.AddScoped<IPricingSettingsService, PricingSettingsService>();
        services.AddScoped<IServiceFeeService, ServiceFeeService>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();
        services.AddScoped<IDashboardStateService, DashboardStateService>();
        services.AddScoped<IBrowserService, BrowserService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IConsentBannerService, ConsentBannerService>();
        services.AddScoped<LeagueDashboardStateService>();
        services.AddScoped<BoostClientService>();
        services.AddScoped<EnterResultsViewModel>();
        services.AddScoped<IPageVisibilityService, PageVisibilityService>();
        services.AddScoped<LiveScorePollingService>();
    }
}