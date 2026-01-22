using Microsoft.JSInterop;

namespace PredictionLeague.Web.Client.Services.Browser;

public class BrowserService : IBrowserService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsDesktop()
    {
        var width = await _jsRuntime.InvokeAsync<int>("blazorInterop.getWindowWidth");
        return width >= 992;
    }

    public async Task<bool> IsTabletOrAbove()
    {
        var width = await _jsRuntime.InvokeAsync<int>("blazorInterop.getWindowWidth");
        return width >= 768;
    }
}