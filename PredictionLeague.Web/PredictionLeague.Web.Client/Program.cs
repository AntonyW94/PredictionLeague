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

        builder.RootComponents.Add<App>("#app");
       
        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7075/") // Your API's base URL
        });
        
        builder.Services.AddBlazoredLocalStorage(); // Add local storage service
        builder.Services.AddAuthorizationCore(); // Add core authorization services

        builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        await builder.Build().RunAsync();
    }
}