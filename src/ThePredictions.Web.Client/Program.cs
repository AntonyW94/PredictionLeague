using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ThePredictions.Web.Client;
using ThePredictions.Web.Client.Authentication;
using ThePredictions.Web.Client.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.RootComponents.Add<App>("#app");
builder.Services.AddClientServices();

builder.Services.AddHttpClient("API", client =>
    {
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    })
    .AddHttpMessageHandler<AuthorizationMessageHandler>()
    .AddHttpMessageHandler<CookieHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

var host = builder.Build();

await host.RunAsync();
