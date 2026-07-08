namespace ThePredictions.Web.Client.Services.Live;

/// <summary>
/// Exposes the browser tab's visibility state (Page Visibility API) so callers
/// can pause background work, such as live-score polling, while the tab is hidden.
/// </summary>
public interface IPageVisibilityService : IAsyncDisposable
{
    /// <summary>True when the tab is currently hidden (backgrounded or minimised).</summary>
    bool IsHidden { get; }

    /// <summary>Raised whenever the tab's visibility changes.</summary>
    event Action? VisibilityChanged;

    /// <summary>
    /// Registers the underlying browser listener. Safe to call more than once;
    /// only the first call wires up the listener.
    /// </summary>
    Task InitialiseAsync();
}
