using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;

namespace PredictionLeague.Web.Client;
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // This line identifies the root component of your app from index.html
        builder.RootComponents.Add<App>("#app");

        // STEP 1: Register our custom message handler.
        // This handler will intercept every HttpClient request and add the auth token.
        builder.Services.AddTransient<AuthTokenHandler>();

        // STEP 2: Configure a named "API" client.
        // This tells the IHttpClientFactory how to create an HttpClient specifically for your API.
        builder.Services.AddHttpClient("API", client =>
        {
            // It sets the BaseAddress so you can use relative URLs like "api/account/details".
            // Make sure this URL matches the SSL port of your API project.
            client.BaseAddress = new Uri("https://localhost:7075/");
        })
        // It attaches your custom handler to this specific client's pipeline.
        .AddHttpMessageHandler<AuthTokenHandler>();

        // STEP 3: Replace the default HttpClient registration.
        // This is the most crucial step. It tells the dependency injector:
        // "Whenever a component asks for a standard HttpClient (@inject HttpClient),
        // don't give it a new, blank one. Instead, use the factory to create the
        // pre-configured client we named 'API'."
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

        // STEP 4: Register your authentication services as before.
        builder.Services.AddBlazoredLocalStorage();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        // Build and run the application.
        await builder.Build().RunAsync();
    }
}
