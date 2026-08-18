using Microsoft.JSInterop;

namespace ThePredictions.Web.Client.Services.Time;

/// <summary>
/// The shared one-second heartbeat, driven by a single JavaScript interval.
/// </summary>
/// <remarks>
/// The interval is started by the first subscriber and stopped by the last, so a page with no countdown on it
/// costs nothing. It reuses the existing <c>blazorInterop.startCountdown</c> timer registry under one fixed id
/// rather than adding a second mechanism beside it.
/// </remarks>
public sealed class CountdownTickService(IJSRuntime jsRuntime) : ICountdownTickService, IAsyncDisposable
{
    // The registry in interop.js is keyed by id, so a constant here is what keeps this to exactly one interval.
    private const string SharedTimerId = "shared-countdown";

    private readonly List<Action> _handlers = [];

    private DotNetObjectReference<CountdownTickService>? _selfReference;

    public async Task SubscribeAsync(Action handler)
    {
        _handlers.Add(handler);

        if (_handlers.Count > 1)
            return;

        _selfReference ??= DotNetObjectReference.Create(this);

        await jsRuntime.InvokeVoidAsync("blazorInterop.startCountdown", _selfReference, nameof(OnTick), SharedTimerId);
    }

    public async Task UnsubscribeAsync(Action handler)
    {
        if (!_handlers.Remove(handler))
            return;

        if (_handlers.Count > 0)
            return;

        await jsRuntime.InvokeVoidAsync("blazorInterop.stopCountdown", SharedTimerId);
    }

    /// <summary>
    /// Fans the tick out to every subscriber.
    /// </summary>
    /// <remarks>
    /// Over a copy, because a countdown that reaches zero unsubscribes from inside its own handler and would
    /// otherwise be mutating the list this is walking.
    /// </remarks>
    [JSInvokable]
    public void OnTick()
    {
        foreach (var handler in _handlers.ToArray())
            handler();
    }

    public async ValueTask DisposeAsync()
    {
        if (_handlers.Count > 0)
        {
            _handlers.Clear();

            try
            {
                await jsRuntime.InvokeVoidAsync("blazorInterop.stopCountdown", SharedTimerId);
            }
            catch (JSDisconnectedException)
            {
                // The browser context has already gone; there is no interval left to clear.
            }
        }

        _selfReference?.Dispose();
        _selfReference = null;
    }
}
