using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using PredictionLeague.Web.Client;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;
using PredictionLeague.Web.Client.Services.Browser;

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

var host = builder.Build();

//var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
//var datadogProvider = new DatadogBrowserLoggerProvider(jsRuntime);
//var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

//loggerFactory.AddProvider(datadogProvider);

await host.RunAsync();
