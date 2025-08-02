using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PredictionLeague.Web.Client;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.RootComponents.Add<App>("#app");
builder.Services.AddClientServices();

builder.Services.AddScoped(sp =>
{
    var cookieHandler = sp.GetRequiredService<CookieHandler>();
    cookieHandler.InnerHandler = new HttpClientHandler();

    return new HttpClient(cookieHandler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
});


await builder.Build().RunAsync();