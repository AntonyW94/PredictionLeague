using Microsoft.JSInterop;

namespace PredictionLeague.Web.Client.Services.Browser;

public class BrowserService(IJSRuntime jsRuntime) : IBrowserService
{
    public async Task<bool> IsDesktop()
    {
        var width = await jsRuntime.InvokeAsync<int>("blazorInterop.getWindowWidth");
        return width >= 992;
    }

    public async Task<bool> IsTabletOrAbove()
    {
        var width = await jsRuntime.InvokeAsync<int>("blazorInterop.getWindowWidth");
        return width >= 768;
    }
}