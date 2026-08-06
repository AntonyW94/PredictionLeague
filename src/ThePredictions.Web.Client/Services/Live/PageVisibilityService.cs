using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace ThePredictions.Web.Client.Services.Live;

/// <summary>
/// Tracks the tab's visibility via the browser Page Visibility API, bridged
/// through <c>blazorInterop.registerVisibilityCallback</c>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
public sealed class PageVisibilityService(IJSRuntime jsRuntime) : IPageVisibilityService
{
    private DotNetObjectReference<PageVisibilityService>? _selfReference;
    private bool _initialised;

    public bool IsHidden { get; private set; }

    public event Action? VisibilityChanged;

    public async Task InitialiseAsync()
    {
        if (_initialised)
            return;

        _initialised = true;
        _selfReference = DotNetObjectReference.Create(this);
        IsHidden = await jsRuntime.InvokeAsync<bool>("blazorInterop.registerVisibilityCallback", _selfReference, nameof(OnVisibilityChanged));
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool isHidden)
    {
        if (IsHidden == isHidden)
            return;

        IsHidden = isHidden;
        VisibilityChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialised)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("blazorInterop.unregisterVisibilityCallback");
            }
            catch (JSDisconnectedException)
            {
                // The circuit or browser context is already gone; nothing to unregister.
            }
        }

        _selfReference?.Dispose();
    }
}
