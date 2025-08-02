using Microsoft.JSInterop;

namespace PredictionLeague.Web.Client.Services.Browser;

public class DatadogBrowserLoggerProvider : ILoggerProvider
{
    private readonly IJSRuntime _jsRuntime;

    public DatadogBrowserLoggerProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ILogger CreateLogger(string categoryName) => new DatadogBrowserLogger(_jsRuntime);

    public void Dispose() { }
}