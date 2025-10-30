using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Services.Browser;
using PredictionLeague.Web.Client.Services.Dashboard;
using PredictionLeague.Web.Client.Services.Leagues;
using PredictionLeague.Web.Client.ViewModels.Admin.Rounds;

namespace PredictionLeague.Web.Client;

public static class DependencyInjection
{
    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddBlazoredLocalStorage();
        services.AddTransient<CookieHandler>();
      
        services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ILeagueService, LeagueService>();
        services.AddScoped<IDashboardStateService, DashboardStateService>();
        services.AddScoped<IBrowserService, BrowserService>(); 
        services.AddScoped<LeagueDashboardStateService>();
        services.AddScoped<EnterResultsViewModel>();
    }
}