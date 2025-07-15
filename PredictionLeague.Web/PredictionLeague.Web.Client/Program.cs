using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddTransient<CookieHandler>();

builder.Services.AddScoped(sp =>
{
    var cookieHandler = sp.GetRequiredService<CookieHandler>();
    cookieHandler.InnerHandler = new HttpClientHandler();

    var config = sp.GetRequiredService<IConfiguration>();
    var apiBaseUrl = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

    var client = new HttpClient(cookieHandler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };

    return client;
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

await builder.Build().RunAsync();