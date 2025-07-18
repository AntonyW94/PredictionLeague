using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Web.Client.Authentication;

namespace PredictionLeague.Web.Client;

public static class DependencyInjection
{
    public static void AddClientServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddTransient<CookieHandler>();
        services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }
}