using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PredictionLeague.Web.Client.Authentication;

namespace PredictionLeague.Web.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7075/") // Your API's base URL
        });

        builder.Services.AddBlazoredLocalStorage(); // Add local storage service
        builder.Services.AddAuthorizationCore(); // Add core authorization services

        builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        var app = builder.Build();

        await app.RunAsync();
    }
}