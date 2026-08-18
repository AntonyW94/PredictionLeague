namespace ThePredictions.Web.Client.Services.Time;

/// <summary>
/// A single one-second heartbeat shared by every countdown on the page.
/// </summary>
/// <remarks>
/// Countdowns used to own their interval each, which was fine while one card carried one of them. The Active
/// Rounds card now shows a lock countdown per match, and two cards can sit side by side on a wide desktop, so
/// that arrangement would have meant up to forty JavaScript timers each making its own interop call into .NET
/// every second. One timer serves them all instead, at a cost that does not grow with the number of matches.
/// </remarks>
public interface ICountdownTickService
{
    /// <summary>Registers a handler to be called once a second, starting the heartbeat if it is not running.</summary>
    Task SubscribeAsync(Action handler);

    /// <summary>Removes a handler, stopping the heartbeat once nothing is listening.</summary>
    Task UnsubscribeAsync(Action handler);
}
