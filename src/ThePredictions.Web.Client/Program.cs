using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using ThePredictions.Web.Client;
using ThePredictions.Web.Client.Authentication;
using ThePredictions.Web.Client.Components;
using ThePredictions.Web.Client.Services.Live;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.RootComponents.Add<App>("#app");
builder.Services.AddClientServices();

// Live-score polling interval is configurable via LivePolling:IntervalSeconds; defaults to 10s.
builder.Services.AddSingleton(new LivePollingOptions
{
    Interval = TimeSpan.FromSeconds(builder.Configuration.GetValue("LivePolling:IntervalSeconds", 10))
});

builder.Services.AddHttpClient("API", client =>
    {
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    })
    .AddHttpMessageHandler<AuthorizationMessageHandler>()
    .AddHttpMessageHandler<CookieHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

var host = builder.Build();

await host.RunAsync();
