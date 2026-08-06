using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace ThePredictions.Web.Client.Services.Browser;

[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
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

    // Bootstrap xxl breakpoint. Used where a tile only has room for its "roomy" two-column
    // layout on genuinely wide screens (e.g. the Active Rounds carousel showing two round
    // cards side by side); below this it falls back to a single, wider card.
    public async Task<bool> IsWideDesktop()
    {
        var width = await jsRuntime.InvokeAsync<int>("blazorInterop.getWindowWidth");
        return width >= 1400;
    }
}
