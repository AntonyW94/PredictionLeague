namespace ThePredictions.Web.Client.Services.Live;

/// <summary>The result of a single polling attempt.</summary>
public enum PollOutcome
{
    /// <summary>Polling has not been started, so there is nothing to do.</summary>
    NotStarted,

    /// <summary>Nothing is live, so polling should stop.</summary>
    NotLive,

    /// <summary>The tab is hidden, so the poll was skipped (polling stays paused, not stopped).</summary>
    Hidden,

    /// <summary>The poll ran and the refresh callback completed.</summary>
    Polled,

    /// <summary>The refresh callback threw; last-known values are kept and polling continues.</summary>
    Failed,

    /// <summary>The poll was cancelled (the service was stopped mid-poll).</summary>
    Cancelled
}
