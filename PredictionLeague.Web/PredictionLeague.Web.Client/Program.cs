using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PredictionLeague.Web.Client;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.RootComponents.Add<App>("#app");
builder.Services.AddClientServices();

builder.Services.AddHttpClient("API", client =>
    {
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    })
    .AddHttpMessageHandler<CookieHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

var host = builder.Build();

await host.RunAsync();
